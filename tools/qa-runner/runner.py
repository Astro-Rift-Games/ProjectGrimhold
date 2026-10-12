"""QA-001 process observation with an explicitly opt-in QA-002 Windows demo."""

import argparse
from datetime import datetime, timezone
import json
import math
from pathlib import Path
import subprocess
import sys
import time
import uuid

POLL_INTERVAL_SECONDS = 0.1
CLEANUP_GRACE_SECONDS = 2.0
DEFAULT_RESULTS_DIR = Path(__file__).resolve().parent / "results"


class ReportIOError(OSError):
    """The runner could not reserve or save its report."""


def positive_duration(value):
    """Accept seconds only if strictly positive and finite."""
    try:
        seconds = float(value)
    except (TypeError, ValueError, OverflowError) as exc:
        raise ValueError("duration must be a positive finite number of seconds") from exc
    if not math.isfinite(seconds) or seconds <= 0:
        raise ValueError("duration must be a positive finite number of seconds")
    return seconds


def write_report(stream, report):
    """Write strict JSON; flush so ordinary write failures reach the caller."""
    payload = json.dumps(report, ensure_ascii=False, allow_nan=False, indent=2) + "\n"
    stream.seek(0)
    stream.write(payload)
    stream.truncate()
    stream.flush()


def reserve_report(directory, report):
    """Exclusively create and preflight a report before starting any process."""
    stream = None
    try:
        directory.mkdir(parents=True, exist_ok=True)
        while True:
            path = directory / f"qa-{uuid.uuid4().hex}.json"
            try:
                stream = path.open("x", encoding="utf-8")
                break
            except FileExistsError:
                continue
        write_report(stream, report)
        return path, stream
    except OSError as exc:
        if stream is not None:
            stream.close()
        raise ReportIOError(f"Cannot prepare report in {directory}: {exc}") from exc


def cleanup(process):
    """Touch only our Popen handle; never find other PIDs or descendants."""
    errors = []
    interrupted = False

    def record(stage, exc):
        nonlocal interrupted
        interrupted = interrupted or isinstance(exc, KeyboardInterrupt)
        errors.append(f"Cleanup {stage}: {type(exc).__name__}: {exc}")

    try:
        if process.poll() is not None:
            return errors, interrupted
    except (Exception, KeyboardInterrupt) as exc:
        record("poll", exc)

    try:
        process.terminate()
        process.wait(timeout=CLEANUP_GRACE_SECONDS)
        return errors, interrupted
    except (Exception, KeyboardInterrupt) as exc:
        record("terminate/wait", exc)

    try:
        process.kill()
        process.wait(timeout=CLEANUP_GRACE_SECONDS)
    except (Exception, KeyboardInterrupt) as exc:
        record("kill/wait", exc)
    return errors, interrupted


def run(executable, duration, results_dir, *, command=None, demo=None, control_factory=None):
    """Return (report, path). `command` is internal argv for stdlib child tests.

    ReportIOError means no reliable final report could be saved. The CLI never
    exposes command arguments; it launches only the resolved executable.
    """
    duration = positive_duration(duration)
    if demo is not None:
        demo.validate_duration(duration)
    executable = Path(executable).resolve()
    results_dir = Path(results_dir).resolve()
    report = {
        "date_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "executable": str(executable),
        "configured_duration_seconds": duration,
        "observed_duration_seconds": 0.0,
        "result": "failure",
        "functional_validation": "not_performed",
        "gameplay_validated": False,
        "actions": [],
        "evidence": [],
        "error": "Run incomplete; final report has not been saved.",
    }
    if demo is not None:
        # Pure stdlib module at import; Win32 and GUI setup occur only in execute.
        from windows_control import Demo, requested_actions
        report["demo_requested"] = True
        report["actions"] = requested_actions(demo)
        if control_factory is None:
            control_factory = Demo
    path, stream = reserve_report(results_dir, report)
    process = None
    control = None
    try:
        with stream:
            report.pop("error")
            try:
                argv = [str(executable)] if command is None else list(command)
                process = subprocess.Popen(argv, shell=False, cwd=str(executable.parent))
                report["pid"] = process.pid
                started = time.monotonic()
                if demo is not None:
                    control = control_factory(process, started + duration, report, path, demo)
                    try:
                        control.execute()
                    finally:
                        report["observed_duration_seconds"] = max(0.0, time.monotonic() - started)
                while True:
                    elapsed = max(0.0, time.monotonic() - started)
                    report["observed_duration_seconds"] = elapsed
                    returncode = process.poll()
                    if returncode is not None:
                        report["returncode"] = returncode
                        report["error"] = f"Process exited early (return code {returncode})."
                        break
                    if elapsed >= duration:
                        report["result"] = "success"
                        break
                    time.sleep(min(POLL_INTERVAL_SECONDS, duration - elapsed))
            except KeyboardInterrupt:
                report["result"] = "interrupted"
                report["error"] = "Observation interrupted by KeyboardInterrupt."
            except Exception as exc:
                report["error"] = f"Launch/observation failed: {type(exc).__name__}: {exc}"
            finally:
                # Keys MUST be released before the owned process is terminated.
                if control is not None:
                    try:
                        errors, interrupted = control.release_keys()
                    except (Exception, KeyboardInterrupt) as exc:
                        errors = [f"Key cleanup failed: {type(exc).__name__}: {exc}"]
                        interrupted = isinstance(exc, KeyboardInterrupt)
                    if errors:
                        report["error"] = "; ".join(filter(None, [report.get("error"), *errors]))
                        if report["result"] != "interrupted":
                            report["result"] = "failure"
                    if interrupted:
                        report["result"] = "interrupted"
                if process is not None:
                    errors, interrupted = cleanup(process)
                    if errors:
                        report["error"] = "; ".join(filter(None, [report.get("error"), *errors]))
                        if report["result"] != "interrupted":
                            report["result"] = "failure"
                    if interrupted:
                        report["result"] = "interrupted"
                    if process.returncode is not None:
                        report["returncode"] = process.returncode
            write_report(stream, report)
    except OSError as exc:
        raise ReportIOError(f"Cannot save final report {path}: {exc}") from exc
    return report, path


def duration_argument(value):
    try:
        return positive_duration(value)
    except ValueError as exc:
        raise argparse.ArgumentTypeError(str(exc)) from exc


def main(argv=None):
    parser = argparse.ArgumentParser(
        description="Observe a build process for a duration; this is not a functional game test."
    )
    parser.add_argument("--exe", required=True, type=Path, help="path to the build executable")
    parser.add_argument("--duration", required=True, type=duration_argument,
                        help="positive finite observation duration in seconds")
    parser.add_argument("--results-dir", type=Path, default=DEFAULT_RESULTS_DIR,
                        help=f"report directory (default: {DEFAULT_RESULTS_DIR})")
    parser.add_argument("--demo-key", help="opt in to one global key press/release then PNG capture (Windows only)")
    parser.add_argument("--window-wait", type=duration_argument, default=None,
                        help="demo-only manual focus/window wait seconds (default 10; maximum 60)")
    parser.add_argument("--key-hold", type=duration_argument, default=None,
                        help="demo-only key hold seconds (default 0.2; maximum 10)")
    parser.add_argument("--capture-delay", type=duration_argument, default=None,
                        help="demo-only delay after release (default 0.2; maximum 30)")
    args = parser.parse_args(argv)
    demo = None
    if args.demo_key is None:
        if any(value is not None for value in (args.window_wait, args.key_hold, args.capture_delay)):
            parser.error("demo timing options require --demo-key")
    else:
        from windows_control import DemoOptions
        try:
            demo = DemoOptions(args.demo_key,
                               window_wait_seconds=args.window_wait if args.window_wait is not None else 10,
                               hold_seconds=args.key_hold if args.key_hold is not None else 0.2,
                               capture_delay_seconds=args.capture_delay if args.capture_delay is not None else 0.2)
            demo.validate_duration(args.duration)
        except ValueError as exc:
            parser.error(str(exc))
    try:
        if demo is None:
            report, path = run(args.exe, args.duration, args.results_dir)
        else:
            report, path = run(args.exe, args.duration, args.results_dir, demo=demo)
    except (OSError, ValueError) as exc:
        print(f"QA runner error: {exc}", file=sys.stderr)
        return 1
    except KeyboardInterrupt:
        print("QA runner interrupted; final report may be incomplete.", file=sys.stderr)
        return 130
    print(f"QA runner: {report['result']}; report: {path}")
    if "error" in report:
        print(report["error"], file=sys.stderr)
    return {"success": 0, "failure": 1, "interrupted": 130}[report["result"]]


if __name__ == "__main__":
    sys.exit(main())
