"""QA-001 runner tests; temporary report directories stay inside qa-runner."""

from contextlib import contextmanager
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
from unittest.mock import patch

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
                [str(Path(sys.executable).resolve())],
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
            with patch.object(runner.time, "monotonic", side_effect=RuntimeError("clock broke")):
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
            runner.run(relative, 0.5, directory)
            launch.assert_called_once_with([str(executable.resolve())], shell=False,
                                           cwd=str(executable.resolve().parent))

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


if __name__ == "__main__":
    unittest.main()
