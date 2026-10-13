"""QA-001 process observation with an explicitly opt-in QA-002 Windows demo."""

import argparse
from datetime import datetime, timezone
import hashlib
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
SCHEMA_VERSION = 1
PHASE_NAMES = ("launch", "observe", "cleanup")
PLAYER_LOG_CHUNK_BYTES = 64 * 1024


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


def reserve_report(directory, report, bind=None):
    """Exclusively create and preflight a report before starting any process.

    The accepted uuid names the file and becomes `report["run_id"]`; `bind`
    (optional) receives it before the preflight write to derive per-run paths.
    """
    stream = None
    try:
        directory.mkdir(parents=True, exist_ok=True)
        while True:
            run_id = uuid.uuid4().hex
            path = directory / f"qa-{run_id}.json"
            try:
                stream = path.open("x", encoding="utf-8")
                break
            except FileExistsError:
                continue
        report["run_id"] = run_id
        if bind is not None:
            bind(run_id)
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


def inspect_player_log(path):
    """Evidence status only; hash in bounded chunks, never raise or return content."""
    status = {"status": "missing", "size_bytes": None, "sha256": None, "error": None}
    digest, size = hashlib.sha256(), 0
    try:
        with open(path, "rb") as stream:
            while True:
                chunk = stream.read(PLAYER_LOG_CHUNK_BYTES)
                if not chunk:
                    break
                digest.update(chunk)
                size += len(chunk)
    except FileNotFoundError:
        return status
    except OSError as exc:
        status.update(status="error", error=f"{type(exc).__name__}: {exc}")
        return status
    status.update(status="present", size_bytes=size, sha256=digest.hexdigest())
    return status


def seconds_since(origin):
    """Launch-relative monotonic seconds; None when unknown. Timing never aborts a run."""
    if origin is None:
        return None
    try:
        return max(0.0, time.monotonic() - origin)
    except Exception:
        return None


def first_failure(report, phase_errors):
    """Earliest failing stage: action errors outrank their enclosing phase."""
    for action in report["actions"]:
        # Key-release errors are cleanup evidence and surface via the cleanup phase.
        if action.get("status") == "error" and action["name"] != "key_release_cleanup":
            return {"stage": action["name"], "error": action.get("error")}
    for name in PHASE_NAMES:
        if name in phase_errors:
            return {"stage": name, "error": phase_errors[name]}
    return None


def run(executable, duration, results_dir, *, command=None, demo=None, control_factory=None):
    """Return (report, path). `command` is internal argv for stdlib child tests.

    ReportIOError means no reliable final report could be saved. The CLI never
    exposes command arguments; it launches only the resolved executable, adding
    only `-logFile <results>/qa-<run_id>-player.log`.
    """
    duration = positive_duration(duration)
    if demo is not None:
        demo.validate_duration(duration)
    executable = Path(executable).resolve()
    results_dir = Path(results_dir).resolve()
    from readiness import initial_report
    report = {
        "schema_version": SCHEMA_VERSION,
        "run_id": None,
        "readiness": initial_report(demo.readiness if demo is not None else None),
        "date_utc": datetime.now(timezone.utc).isoformat().replace("+00:00", "Z"),
        "executable": str(executable),
        "configured_duration_seconds": duration,
        "observed_duration_seconds": 0.0,
        "result": "failure",
        "functional_validation": "not_performed",
        "gameplay_validated": False,
        "actions": [],
        "evidence": [],
        "player_log": {"requested": command is None, "path": None,
                       "status": "pending" if command is None else "not_requested",
                       "size_bytes": None, "sha256": None, "error": None},
        "phases": [{"name": name, "status": "skipped",
                    "started_at_seconds": None, "ended_at_seconds": None} for name in PHASE_NAMES],
        "first_failure": None,
        "error": "Run incomplete; final report has not been saved.",
    }
    if demo is not None:
        # Pure stdlib module at import; Win32 and GUI setup occur only in execute.
        from windows_control import Demo, requested_actions
        report["demo_requested"] = True
        report["actions"] = requested_actions(demo)
        if control_factory is None:
            control_factory = Demo
    def bind_log_path(run_id):
        if command is None:
            report["player_log"]["path"] = str(results_dir / f"qa-{run_id}-player.log")

    path, stream = reserve_report(results_dir, report, bind_log_path)
    phases = {phase["name"]: phase for phase in report["phases"]}
    phase_errors = {}
    stage = "launch"
    origin = None
    process = None
    control = None

    def end_phase(name, status, message=None):
        phases[name].update(status=status, ended_at_seconds=seconds_since(origin))
        if message is not None:
            phase_errors[name] = message

    try:
        with stream:
            report.pop("error")
            try:
                if command is None:
                    argv = [str(executable), "-logFile", report["player_log"]["path"]]
                else:
                    argv = list(command)
                origin = time.monotonic()
                phases["launch"]["started_at_seconds"] = 0.0
                process = subprocess.Popen(argv, shell=False, cwd=str(executable.parent))
                report["pid"] = process.pid
                started = time.monotonic()
                phases["launch"].update(status="completed", ended_at_seconds=started - origin)
                phases["observe"]["started_at_seconds"] = started - origin
                stage = "observe"
                if demo is not None:
                    control = control_factory(process, started + duration, report, path, demo)
                    control.origin = origin
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
                        end_phase("observe", "error", report["error"])
                        break
                    if elapsed >= duration:
                        report["result"] = "success"
                        end_phase("observe", "completed")
                        break
                    time.sleep(min(POLL_INTERVAL_SECONDS, duration - elapsed))
            except KeyboardInterrupt:
                report["result"] = "interrupted"
                report["error"] = "Observation interrupted by KeyboardInterrupt."
                end_phase(stage, "interrupted", report["error"])
            except Exception as exc:
                report["error"] = f"Launch/observation failed: {type(exc).__name__}: {exc}"
                end_phase(stage, "error", report["error"])
            finally:
                cleanup_errors, cleanup_interrupted = [], False
                if process is not None:
                    phases["cleanup"]["started_at_seconds"] = seconds_since(origin)
                if (report["readiness"]["requested"]
                        and report["readiness"].get("error") == "Readiness has not completed."):
                    report["readiness"].update(state="blocked", error=report.get("error", "Readiness did not complete."))
                # Keys MUST be released before the owned process is terminated.
                if control is not None:
                    try:
                        errors, interrupted = control.release_keys()
                    except (Exception, KeyboardInterrupt) as exc:
                        errors = [f"Key cleanup failed: {type(exc).__name__}: {exc}"]
                        interrupted = isinstance(exc, KeyboardInterrupt)
                    cleanup_errors += errors
                    cleanup_interrupted |= interrupted
                    if errors:
                        report["error"] = "; ".join(filter(None, [report.get("error"), *errors]))
                        if report["result"] != "interrupted":
                            report["result"] = "failure"
                    if interrupted:
                        report["result"] = "interrupted"
                if process is not None:
                    errors, interrupted = cleanup(process)
                    cleanup_errors += errors
                    cleanup_interrupted |= interrupted
                    if errors:
                        report["error"] = "; ".join(filter(None, [report.get("error"), *errors]))
                        if report["result"] != "interrupted":
                            report["result"] = "failure"
                    if interrupted:
                        report["result"] = "interrupted"
                    if process.returncode is not None:
                        report["returncode"] = process.returncode
                    end_phase("cleanup", "interrupted" if cleanup_interrupted
                              else "error" if cleanup_errors else "completed",
                              "; ".join(cleanup_errors) if cleanup_errors else None)
            if report["player_log"]["requested"]:
                # Inspect only after cleanup so the owned process cannot still be writing.
                try:
                    report["player_log"].update(inspect_player_log(report["player_log"]["path"]))
                except KeyboardInterrupt:
                    report["player_log"].update(status="error", error="Interrupted while reading the player log.")
                    report["result"] = "interrupted"
            report["first_failure"] = first_failure(report, phase_errors)
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
    parser.add_argument("--acquire-focus", action="store_true",
                        help="demo-only opt-in: request foreground focus for the owned window "
                             "(launch the runner from the foreground interactive context)")
    parser.add_argument("--window-wait", type=duration_argument, default=None,
                        help="demo-only manual focus/window wait seconds (default 10; maximum 60)")
    parser.add_argument("--key-hold", type=duration_argument, default=None,
                        help="demo-only key hold seconds (default 0.2; maximum 10)")
    parser.add_argument("--capture-delay", type=duration_argument, default=None,
                        help="demo-only delay after release (default 0.2; maximum 30)")
    parser.add_argument("--ready-reference", type=Path, help="opt-in same-scale main-menu PNG template")
    parser.add_argument("--ready-timeout", type=float, default=None, help="readiness seconds, 0.05..60 (default 10)")
    parser.add_argument("--ready-interval", type=float, default=None, help="poll seconds, 0.02..5 (default 0.2)")
    parser.add_argument("--ready-tolerance", type=float, default=None, help="RGB error tolerance, 1..20 (default 12)")
    parser.add_argument("--ready-count", type=int, default=None, help="stable consecutive frames, 2..10 (default 3)")
    args = parser.parse_args(argv)
    ready = None
    ready_timings = (args.ready_timeout, args.ready_interval, args.ready_tolerance, args.ready_count)
    if args.ready_reference is not None or any(value is not None for value in ready_timings):
        if args.demo_key is None or args.ready_reference is None:
            parser.error("readiness options require --demo-key and --ready-reference")
        from readiness import ReadyOptions
        try:
            ready = ReadyOptions(args.ready_reference,
                                 timeout_seconds=args.ready_timeout if args.ready_timeout is not None else 10,
                                 interval_seconds=args.ready_interval if args.ready_interval is not None else 0.2,
                                 tolerance=args.ready_tolerance if args.ready_tolerance is not None else 12,
                                 consistent_count=args.ready_count if args.ready_count is not None else 3)
        except ValueError as exc:
            parser.error(str(exc))
    demo = None
    if args.demo_key is None:
        if any(value is not None for value in (args.window_wait, args.key_hold, args.capture_delay)):
            parser.error("demo timing options require --demo-key")
        if args.acquire_focus:
            parser.error("--acquire-focus requires --demo-key")
    else:
        from windows_control import DemoOptions
        try:
            demo = DemoOptions(args.demo_key,
                               window_wait_seconds=args.window_wait if args.window_wait is not None else 10,
                               hold_seconds=args.key_hold if args.key_hold is not None else 0.2,
                               capture_delay_seconds=args.capture_delay if args.capture_delay is not None else 0.2,
                               readiness=ready, acquire_focus=args.acquire_focus)
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
