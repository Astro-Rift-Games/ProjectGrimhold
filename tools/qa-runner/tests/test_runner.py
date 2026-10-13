"""QA-001 runner tests; temporary report directories stay inside qa-runner."""

from contextlib import contextmanager
import hashlib
import importlib
import io
import json
from types import SimpleNamespace
import math
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

QA_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(QA_ROOT))


class Clock:
    def __init__(self):
        self.now = 0.0

    def monotonic(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds


class FakeProcess:
    def __init__(self):
        self.pid = 1234
        self.returncode = None
        self.terminated = False
        self.killed = False
        self.waits = []

    def poll(self):
        return self.returncode

    def terminate(self):
        self.terminated = True
        self.returncode = -15

    def kill(self):
        self.killed = True
        self.returncode = -9

    def wait(self, timeout):
        self.waits.append(timeout)
        return self.returncode


class RunnerTests(unittest.TestCase):
    @contextmanager
    def simulate(self, process=None):
        runner = importlib.import_module("runner")
        process = process or FakeProcess()
        clock = Clock()
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process) as launch, \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                yield runner, process, Path(directory), launch

    def test_full_duration_is_success_and_cleans_only_launched_handle(self):
        runner = importlib.import_module("runner")
        clock = Clock()
        owned = FakeProcess()
        unowned = FakeProcess()
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=owned) as launch, \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                report, report_path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["configured_duration_seconds"], 0.5)
            self.assertEqual(report["observed_duration_seconds"], 0.5)
            self.assertTrue(report_path.is_file())
            self.assertTrue(owned.terminated)
            self.assertEqual(owned.waits, [runner.CLEANUP_GRACE_SECONDS])
            self.assertFalse(owned.killed)
            self.assertFalse(unowned.terminated)
            self.assertFalse(unowned.killed)
            launch.assert_called_once_with(
                [str(Path(sys.executable).resolve()), "-logFile",
                 str(Path(directory).resolve() / f"qa-{report['run_id']}-player.log")],
                shell=False,
                cwd=str(Path(sys.executable).resolve().parent),
            )

    def test_early_exit_zero_and_nonzero_are_failures(self):
        for returncode in (0, 7):
            with self.subTest(returncode=returncode), self.simulate() as (runner, process, directory, _):
                process.returncode = returncode
                report, _ = runner.run(sys.executable, 1, directory)
                self.assertEqual(report["result"], "failure")
                self.assertIn("early", report["error"].lower())
                self.assertEqual(report["returncode"], returncode)
                self.assertEqual(report["observed_duration_seconds"], 0)
                self.assertFalse(process.terminated)
                self.assertFalse(process.killed)

    def test_exit_during_observation_is_failure(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "poll", side_effect=[None, 0, 0]):
                report, _ = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertGreater(report["observed_duration_seconds"], 0)
            self.assertLess(report["observed_duration_seconds"], 1)

    def test_launch_error_is_saved_without_cleanup(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect = OSError("launch denied")
            report, path = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("launch denied", report["error"])
            self.assertEqual(report["observed_duration_seconds"], 0)
            self.assertNotIn("pid", report)
            self.assertFalse(process.terminated)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_observation_exception_still_cleans_up(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(runner.time, "sleep", side_effect=RuntimeError("observation broke")):
                report, _ = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("observation broke", report["error"])
            self.assertTrue(process.terminated)
            self.assertTrue(process.waits)

    def test_interruption_still_cleans_up_and_is_reported(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(runner.time, "sleep", side_effect=KeyboardInterrupt):
                report, path = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "interrupted")
            self.assertIn("interrupt", report["error"].lower())
            self.assertTrue(process.terminated)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_monotonic_exception_after_launch_still_cleans_up(self):
        with self.simulate() as (runner, process, directory, _):
            reads = iter([0.0])

            def broken_after_launch():
                try:
                    return next(reads)
                except StopIteration:
                    raise RuntimeError("clock broke") from None

            with patch.object(runner.time, "monotonic", broken_after_launch):
                report, _ = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("clock broke", report["error"])
            self.assertTrue(process.terminated)

    def test_terminate_error_uses_kill_and_cannot_report_success(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "terminate", side_effect=OSError("terminate denied")):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("terminate denied", report["error"])
            self.assertTrue(process.killed)
            self.assertEqual(process.waits, [runner.CLEANUP_GRACE_SECONDS])

    def test_terminate_timeout_uses_bounded_kill_wait_and_is_failure(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "wait", side_effect=[subprocess.TimeoutExpired("exe", 2), -9]) as wait:
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("timeout", report["error"].lower())
            self.assertTrue(process.killed)
            self.assertEqual(wait.call_count, 2)
            for call in wait.call_args_list:
                self.assertEqual(call.kwargs, {"timeout": runner.CLEANUP_GRACE_SECONDS})

    def test_kill_timeout_is_reported_without_unbounded_wait(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "wait", side_effect=subprocess.TimeoutExpired("exe", 2)) as wait:
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("kill/wait", report["error"])
            self.assertEqual(wait.call_count, 2)
            self.assertTrue(process.killed)

    def test_kill_error_is_reported(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "terminate", side_effect=OSError("terminate denied")), \
                    patch.object(process, "kill", side_effect=OSError("kill denied")):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("terminate denied", report["error"])
            self.assertIn("kill denied", report["error"])

    def test_cleanup_poll_error_does_not_skip_termination(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "poll", side_effect=OSError("poll denied")):
                report, _ = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("poll denied", report["error"])
            self.assertTrue(process.terminated)

    def test_interruption_during_cleanup_uses_kill_and_preserves_interrupted_status(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "terminate", side_effect=KeyboardInterrupt):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "interrupted")
            self.assertTrue(process.killed)

    def test_duration_validation_rejects_invalid_values_before_launch(self):
        for value in (0, -1, math.nan, math.inf, -math.inf, "garbage"):
            with self.subTest(value=value), self.simulate() as (runner, _, directory, launch):
                with self.assertRaises(ValueError):
                    runner.run(sys.executable, value, directory)
                launch.assert_not_called()
                self.assertEqual(list(directory.iterdir()), [])

    def test_cli_validation_is_exit_two(self):
        runner = importlib.import_module("runner")
        cases = [[], ["--exe", sys.executable],
                 ["--exe", sys.executable, "--duration", "nan"],
                 ["--exe", sys.executable, "--duration", "0"],
                 ["--exe", sys.executable, "--duration", "1", "--command", "test"]]
        for args in cases:
            with self.subTest(args=args), patch("sys.stderr", new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as raised:
                    runner.main(args)
                self.assertEqual(raised.exception.code, 2)

    def test_cli_acquire_focus_requires_demo_key_and_reaches_demo_options(self):
        runner = importlib.import_module("runner")
        base = ["--exe", sys.executable, "--duration", "2"]
        with patch("sys.stderr", new_callable=io.StringIO) as stderr:
            with self.assertRaises(SystemExit) as raised:
                runner.main(base + ["--acquire-focus"])
            self.assertEqual(raised.exception.code, 2)
            self.assertIn("--acquire-focus requires --demo-key", stderr.getvalue())
        for extra, expected in (([], False), (["--acquire-focus"], True)):
            with self.subTest(extra=extra), \
                    patch.object(runner, "run", return_value=({"result": "success"}, Path("r.json"))) as run, \
                    patch("sys.stdout", new_callable=io.StringIO):
                self.assertEqual(runner.main(base + ["--demo-key", "w"] + extra), 0)
                self.assertIs(run.call_args.kwargs["demo"].acquire_focus, expected)

    def test_json_schema_utc_finite_and_unique_reports(self):
        with self.simulate() as (runner, _, directory, _):
            first_report, first_path = runner.run(sys.executable, 0.5, directory)
            second_report, second_path = runner.run(sys.executable, 0.5, directory)
            self.assertNotEqual(first_path, second_path)
            self.assertEqual(json.loads(first_path.read_text(encoding="utf-8")), first_report)
            self.assertEqual(json.loads(second_path.read_text(encoding="utf-8")), second_report)
            self.assertTrue(first_report["date_utc"].endswith("Z"))
            self.assertEqual(first_report["executable"], str(Path(sys.executable).resolve()))
            self.assertEqual(first_report["pid"], 1234)
            self.assertTrue(math.isfinite(first_report["observed_duration_seconds"]))
            self.assertNotIn("error", first_report)
            json.dumps(first_report, allow_nan=False)

    def test_report_name_collision_does_not_overwrite_existing_file(self):
        with self.simulate() as (runner, _, directory, _):
            existing = directory / "qa-existing.json"
            existing.write_text('{"keep": true}', encoding="utf-8")
            with patch.object(runner.uuid, "uuid4", side_effect=[
                    SimpleNamespace(hex="existing"), SimpleNamespace(hex="fresh")]):
                report, path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(existing.read_text(encoding="utf-8"), '{"keep": true}')
            self.assertEqual(path.name, "qa-fresh.json")
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_launch_interruption_is_saved_without_touching_an_unowned_handle(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect = KeyboardInterrupt
            report, _ = runner.run(sys.executable, 1, directory)
            self.assertEqual(report["result"], "interrupted")
            self.assertNotIn("pid", report)
            self.assertFalse(process.terminated)
            self.assertFalse(process.killed)

    def test_preflight_directory_error_prevents_launch(self):
        with self.simulate() as (runner, _, directory, launch):
            blocker = directory / "not-a-directory"
            blocker.write_text("keep", encoding="utf-8")
            with self.assertRaises(runner.ReportIOError):
                runner.run(sys.executable, 1, blocker)
            launch.assert_not_called()
            self.assertEqual(blocker.read_text(encoding="utf-8"), "keep")

    def test_preflight_open_error_prevents_launch(self):
        with self.simulate() as (runner, _, directory, launch):
            with patch.object(Path, "open", side_effect=PermissionError("report denied")):
                with self.assertRaisesRegex(runner.ReportIOError, "report denied"):
                    runner.run(sys.executable, 1, directory)
            launch.assert_not_called()

    def test_preflight_write_error_prevents_launch(self):
        with self.simulate() as (runner, _, directory, launch):
            with patch.object(runner, "write_report", side_effect=OSError("disk full")):
                with self.assertRaisesRegex(runner.ReportIOError, "disk full"):
                    runner.run(sys.executable, 1, directory)
            launch.assert_not_called()

    def test_final_report_write_error_happens_after_cleanup(self):
        with self.simulate() as (runner, process, directory, _):
            original_write = runner.write_report
            count = 0

            def fail_second_write(stream, report):
                nonlocal count
                count += 1
                if count == 2:
                    raise OSError("disk full")
                original_write(stream, report)

            with patch.object(runner, "write_report", side_effect=fail_second_write):
                with self.assertRaisesRegex(runner.ReportIOError, "disk full"):
                    runner.run(sys.executable, 0.5, directory)
            self.assertTrue(process.terminated)
            self.assertTrue(process.waits)

    def test_executable_with_spaces_unicode_and_relative_path_is_single_argument(self):
        with self.simulate() as (runner, _, directory, launch):
            executable = directory / "Grím hold 游戏" / "Game build.exe"
            executable.parent.mkdir()
            executable.write_bytes(b"fake executable")
            relative = executable.relative_to(Path.cwd())
            report, _ = runner.run(relative, 0.5, directory)
            launch.assert_called_once_with(
                [str(executable.resolve()), "-logFile",
                 str(Path(directory).resolve() / f"qa-{report['run_id']}-player.log")],
                shell=False, cwd=str(executable.resolve().parent))

    def test_cli_exit_codes_for_results_and_report_errors(self):
        runner = importlib.import_module("runner")
        args = ["--exe", sys.executable, "--duration", "1"]
        for result, expected in (("success", 0), ("failure", 1), ("interrupted", 130)):
            with self.subTest(result=result), patch.object(runner, "run", return_value=({"result": result}, Path("report.json"))), \
                    patch("sys.stdout", new_callable=io.StringIO):
                self.assertEqual(runner.main(args), expected)
        with patch.object(runner, "run", side_effect=runner.ReportIOError("report denied")), \
                patch("sys.stderr", new_callable=io.StringIO) as stderr:
            self.assertEqual(runner.main(args), 1)
            self.assertIn("report denied", stderr.getvalue())

    def test_real_missing_executable_is_a_clean_failure_report(self):
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            executable = Path(directory) / "missing executable.exe"
            report, path = runner.run(executable, 1, directory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("Launch/observation failed", report["error"])
            self.assertNotIn("pid", report)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_real_short_lived_stdlib_child_zero_and_nonzero_fail(self):
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            for code in (0, 7):
                with self.subTest(code=code):
                    report, _ = runner.run(sys.executable, 10, directory,
                                           command=[sys.executable, "-c", f"raise SystemExit({code})"])
                    self.assertEqual(report["result"], "failure")
                    self.assertEqual(report["returncode"], code)
                    self.assertLess(report["observed_duration_seconds"], 10)

    def test_real_stdlib_sleeping_child_reaches_duration_and_is_reaped(self):
        runner = importlib.import_module("runner")
        handles = []
        real_popen = subprocess.Popen

        def capture(*args, **kwargs):
            process = real_popen(*args, **kwargs)
            handles.append(process)
            return process

        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", side_effect=capture):
                report, _ = runner.run(sys.executable, 0.2, directory,
                                       command=[sys.executable, "-c", "import time; time.sleep(30)"])
            self.assertEqual(report["result"], "success")
            self.assertGreaterEqual(report["observed_duration_seconds"], 0.2)
            self.assertEqual(len(handles), 1)
            self.assertIsNotNone(handles[0].poll())


class ReportV1Tests(unittest.TestCase):
    """E3 minimal: versioned report, per-run player log, phases, first_failure."""

    simulate = RunnerTests.simulate

    def log_path(self, directory, report):
        return Path(directory).resolve() / f"qa-{report['run_id']}-player.log"

    @staticmethod
    def writing_launch(process, content=None, directory=False):
        """Fake Popen that records argv and optionally creates the requested log."""
        calls = []

        def launch(argv, **kwargs):
            calls.append(argv)
            if "-logFile" in argv:
                target = Path(argv[argv.index("-logFile") + 1])
                if directory:
                    target.mkdir()
                elif content is not None:
                    target.write_bytes(content)
            return process

        return launch, calls

    def test_schema_version_and_run_id_match_report_filename(self):
        with self.simulate() as (runner, _, directory, _):
            report, path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["schema_version"], 1)
            self.assertRegex(report["run_id"], r"^[0-9a-f]{32}$")
            self.assertEqual(path.name, f"qa-{report['run_id']}.json")
            saved = json.loads(path.read_text(encoding="utf-8"))
            self.assertEqual(saved, report)
            self.assertEqual(saved["schema_version"], 1)

    def test_legacy_fields_keep_their_semantics(self):
        with self.simulate() as (runner, _, directory, _):
            report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["functional_validation"], "not_performed")
            self.assertIs(report["gameplay_validated"], False)
            self.assertEqual((report["actions"], report["evidence"]), ([], []))
            self.assertNotIn("error", report)

    def test_report_name_collision_reuses_single_generated_run_id(self):
        with self.simulate() as (runner, _, directory, _):
            (directory / "qa-existing.json").write_text("{}", encoding="utf-8")
            with patch.object(runner.uuid, "uuid4", side_effect=[
                    SimpleNamespace(hex="existing"), SimpleNamespace(hex="fresh")]):
                report, path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["run_id"], "fresh")
            self.assertEqual(path.name, "qa-fresh.json")
            self.assertEqual(report["player_log"]["path"], str(self.log_path(directory, report)))

    def test_real_executable_gets_absolute_log_file_argument_only(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, calls = self.writing_launch(process)
            executable = directory / "Grim hold" / "Game build.exe"
            executable.parent.mkdir()
            executable.write_bytes(b"x")
            report, _ = runner.run(executable, 0.5, directory)
            log = self.log_path(directory, report)
            self.assertEqual(calls, [[str(executable.resolve()), "-logFile", str(log)]])
            self.assertTrue(log.is_absolute())
            self.assertEqual(log.name, f"qa-{report['run_id']}-player.log")
            self.assertEqual(report["player_log"]["path"], str(log))

    def test_internal_command_child_gets_no_log_file_and_is_not_requested(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, calls = self.writing_launch(process)
            report, _ = runner.run(sys.executable, 0.5, directory, command=["child", "--flag"])
            self.assertEqual(calls, [["child", "--flag"]])
            self.assertEqual(report["player_log"], {
                "requested": False, "path": None, "status": "not_requested",
                "size_bytes": None, "sha256": None, "error": None})

    def test_player_log_present_records_size_and_sha256_only(self):
        content = b"Unity player log\n" * 10000 + "ñ".encode("utf-8")
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, _ = self.writing_launch(process, content)
            report, path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["player_log"], {
                "requested": True, "path": str(self.log_path(directory, report)),
                "status": "present", "size_bytes": len(content),
                "sha256": hashlib.sha256(content).hexdigest(), "error": None})
            self.assertEqual(report["result"], "success")
            self.assertNotIn("Unity player log", path.read_text(encoding="utf-8"))

    def test_player_log_is_inspected_only_after_process_cleanup(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, _ = self.writing_launch(process, b"log")
            events = []
            original = runner.inspect_player_log

            def terminate():
                events.append("terminate")
                process.returncode = -15

            def inspect(*args):
                events.append("inspect")
                return original(*args)

            with patch.object(process, "terminate", side_effect=terminate), \
                    patch.object(runner, "inspect_player_log", side_effect=inspect):
                runner.run(sys.executable, 0.5, directory)
            self.assertEqual(events, ["terminate", "inspect"])

    def test_missing_player_log_is_visible_but_does_not_change_result(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, _ = self.writing_launch(process)
            report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "success")
            self.assertNotIn("error", report)
            self.assertIsNone(report["first_failure"])
            self.assertIs(report["player_log"]["requested"], True)
            self.assertEqual(report["player_log"]["status"], "missing")
            self.assertIsNone(report["player_log"]["size_bytes"])
            self.assertIsNone(report["player_log"]["sha256"])

    def test_player_log_read_error_is_reported_without_raising_or_changing_result(self):
        with self.simulate() as (runner, process, directory, launch):
            # A directory at the log path is a genuine OSError on open().
            launch.side_effect, _ = self.writing_launch(process, directory=True)
            report, path = runner.run(sys.executable, 0.5, directory)
            log = report["player_log"]
            self.assertEqual(report["result"], "success")
            self.assertEqual(log["status"], "error")
            self.assertTrue(log["error"])
            self.assertEqual((log["size_bytes"], log["sha256"]), (None, None))
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_player_log_mid_read_oserror_is_reported(self):
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, _ = self.writing_launch(process, b"abc")
            broken = Mock()
            broken.__enter__ = Mock(return_value=broken)
            broken.__exit__ = Mock(return_value=False)
            broken.read.side_effect = OSError("read denied")
            with patch.object(runner, "open", create=True, return_value=broken):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["player_log"]["status"], "error")
            self.assertIn("read denied", report["player_log"]["error"])
            self.assertIsNone(report["player_log"]["sha256"])

    def test_phases_for_healthy_run_use_launch_relative_monotonic_seconds(self):
        with self.simulate() as (runner, _, directory, _):
            report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["phases"], [
                {"name": "launch", "status": "completed", "started_at_seconds": 0.0, "ended_at_seconds": 0.0},
                {"name": "observe", "status": "completed", "started_at_seconds": 0.0, "ended_at_seconds": 0.5},
                {"name": "cleanup", "status": "completed", "started_at_seconds": 0.5, "ended_at_seconds": 0.5}])
            self.assertIsNone(report["first_failure"])

    def test_launch_origin_is_read_before_popen(self):
        with self.simulate() as (runner, process, directory, launch):
            clock = Clock()
            clock.now = 100.0

            def slow_launch(argv, **kwargs):
                clock.now += 0.25
                return process

            launch.side_effect = slow_launch
            with patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                report, _ = runner.run(sys.executable, 0.5, directory)
            launch_phase, observe_phase = report["phases"][:2]
            self.assertEqual((launch_phase["started_at_seconds"], launch_phase["ended_at_seconds"]), (0.0, 0.25))
            self.assertEqual(observe_phase["started_at_seconds"], 0.25)
            self.assertEqual(report["observed_duration_seconds"], 0.5)

    def test_early_exit_phases_and_first_failure_are_observe(self):
        with self.simulate() as (runner, process, directory, _):
            process.returncode = 7
            report, _ = runner.run(sys.executable, 1, directory)
            statuses = {p["name"]: p["status"] for p in report["phases"]}
            self.assertEqual(statuses, {"launch": "completed", "observe": "error", "cleanup": "completed"})
            self.assertEqual(report["first_failure"], {"stage": "observe", "error": report["error"]})
            self.assertIn("return code 7", report["first_failure"]["error"])

    def test_keyboard_interrupt_phases_and_first_failure(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(runner.time, "sleep", side_effect=KeyboardInterrupt):
                report, path = runner.run(sys.executable, 1, directory)
            statuses = {p["name"]: p["status"] for p in report["phases"]}
            self.assertEqual(statuses, {"launch": "completed", "observe": "interrupted", "cleanup": "completed"})
            self.assertEqual(report["first_failure"]["stage"], "observe")
            self.assertIn("KeyboardInterrupt", report["first_failure"]["error"])
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_launch_failure_skips_observe_and_cleanup(self):
        for exception, status in ((OSError("launch denied"), "error"), (KeyboardInterrupt(), "interrupted")):
            with self.subTest(status=status), self.simulate() as (runner, _, directory, launch):
                launch.side_effect = exception
                report, _ = runner.run(sys.executable, 1, directory)
                phases = {p["name"]: p for p in report["phases"]}
                self.assertEqual(phases["launch"]["status"], status)
                self.assertEqual(phases["launch"]["started_at_seconds"], 0.0)
                self.assertEqual(phases["observe"], {"name": "observe", "status": "skipped",
                                                     "started_at_seconds": None, "ended_at_seconds": None})
                self.assertEqual(phases["cleanup"]["status"], "skipped")
                self.assertIsNone(phases["cleanup"]["started_at_seconds"])
                self.assertEqual(report["first_failure"]["stage"], "launch")
                self.assertEqual(report["player_log"]["status"], "missing")

    def test_cleanup_error_is_first_failure_only_when_nothing_failed_earlier(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "terminate", side_effect=OSError("terminate denied")):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual({p["name"]: p["status"] for p in report["phases"]},
                             {"launch": "completed", "observe": "completed", "cleanup": "error"})
            self.assertEqual(report["first_failure"]["stage"], "cleanup")
            self.assertIn("terminate denied", report["first_failure"]["error"])
        with self.simulate() as (runner, process, directory, _):
            with patch.object(runner.time, "sleep", side_effect=KeyboardInterrupt), \
                    patch.object(process, "terminate", side_effect=OSError("terminate denied")):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["first_failure"]["stage"], "observe")
            self.assertEqual(report["phases"][2]["status"], "error")

    def test_cleanup_interruption_marks_cleanup_interrupted(self):
        with self.simulate() as (runner, process, directory, _):
            with patch.object(process, "terminate", side_effect=KeyboardInterrupt):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["phases"][2]["status"], "interrupted")
            self.assertEqual(report["first_failure"]["stage"], "cleanup")

    def test_unavailable_report_directory_still_raises_without_partial_claims(self):
        with self.simulate() as (runner, _, directory, launch):
            blocker = directory / "not-a-directory"
            blocker.write_text("keep", encoding="utf-8")
            with self.assertRaises(runner.ReportIOError):
                runner.run(sys.executable, 1, blocker)
            launch.assert_not_called()
            self.assertEqual(sorted(p.name for p in directory.iterdir()), ["not-a-directory"])

    def test_preflight_report_declares_pending_log_and_skipped_phases(self):
        with self.simulate() as (runner, process, directory, launch):
            snapshots = []

            def observe_preflight(argv, **kwargs):
                (path,) = directory.glob("qa-*.json")
                snapshots.append(json.loads(path.read_text(encoding="utf-8")))
                return process

            launch.side_effect = observe_preflight
            report, _ = runner.run(sys.executable, 0.5, directory)
            early = snapshots[0]
            self.assertEqual((early["schema_version"], early["run_id"]), (1, report["run_id"]))
            self.assertEqual(early["player_log"]["status"], "pending")
            self.assertEqual({p["status"] for p in early["phases"]}, {"skipped"})
            self.assertIsNone(early["first_failure"])
            self.assertIn("error", early)

    def test_stdout_file_and_environment_secrets_never_reach_the_report(self):
        secret = "QA_SYNTHETIC_SECRET_7f3a"
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            leak = Path(directory) / "leak.txt"
            code = ("import sys, time\n"
                    f"print({secret!r}); sys.stdout.flush()\n"
                    f"open({str(leak)!r}, 'w').write({secret!r})\n"
                    "time.sleep(30)\n")
            real_popen = subprocess.Popen

            def quiet(*args, **kwargs):
                return real_popen(*args, stdout=subprocess.DEVNULL, **kwargs)

            with patch.dict("os.environ", {"QA_SYNTHETIC_ENV_SECRET": secret}), \
                    patch.object(runner.subprocess, "Popen", side_effect=quiet):
                report, path = runner.run(sys.executable, 1.0, directory,
                                          command=[sys.executable, "-c", code])
            self.assertEqual(leak.read_text(), secret)
            self.assertEqual(report["result"], "success")
            serialized = path.read_text(encoding="utf-8")
            self.assertNotIn(secret, serialized)
            self.assertNotIn(secret, json.dumps(report))
            self.assertNotIn("QA_SYNTHETIC_ENV_SECRET", serialized)

    def test_log_content_with_secret_is_hashed_never_copied(self):
        secret = "QA_SYNTHETIC_SECRET_7f3a"
        with self.simulate() as (runner, process, directory, launch):
            launch.side_effect, _ = self.writing_launch(process, secret.encode())
            report, path = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["player_log"]["sha256"], hashlib.sha256(secret.encode()).hexdigest())
            self.assertNotIn(secret, path.read_text(encoding="utf-8"))


if __name__ == "__main__":
    unittest.main()
