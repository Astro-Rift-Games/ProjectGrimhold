"""QA-002: simulated Win32/GUI only; never import PyAutoGUI or use a desktop."""
from contextlib import contextmanager
import importlib
import importlib.util
import ctypes
from ctypes import wintypes
import io
import json
from pathlib import Path
import sys
import struct
import tempfile
import zlib
import unittest
from types import SimpleNamespace
from unittest.mock import Mock, patch

from test_runner import Clock, FakeProcess, QA_ROOT


class FailSafeException(Exception):
    pass


class ScriptedClock(Clock):
    """Advance between clock reads and reject negative sleeps like time.sleep."""
    def __init__(self, readings):
        super().__init__()
        self.readings = iter(readings)
        self.sleeps = []

    def monotonic(self):
        self.now = next(self.readings, self.now)
        return self.now

    def sleep(self, seconds):
        if seconds < 0:
            raise ValueError("sleep length must be non-negative")
        self.sleeps.append(seconds)
        super().sleep(seconds)


class Windows:
    def __init__(self):
        self.handles = [101]
        self.pid = 1234
        self.focus = 101
        self.valid = True
        self.region = (10, 20, 320, 240)
        self.pressed = set()
        self.pixel_context = False

    @contextmanager
    def physical_pixels(self):
        self.pixel_context = True
        try:
            yield
        finally:
            self.pixel_context = False

    def owned_windows(self, pid):
        return list(self.handles) if self.pid == pid else []

    def validate(self, hwnd, pid):
        if not self.valid or self.pid != pid or self.handles != [hwnd]:
            raise RuntimeError("invalid HWND/PID or ambiguous window")

    def foreground(self):
        return self.focus

    def window_region(self, hwnd):
        x, y, width, height = self.region
        if x < 0 or y < 0 or width <= 0 or height <= 0 or x + width > 1920 or y + height > 1080:
            raise RuntimeError("outside primary monitor")
        return self.region

    def physically_held(self, key):
        return key in self.pressed


class Image:
    def __init__(self, gui):
        self.gui = gui

    def save(self, stream, format):
        if self.gui.save_error:
            raise self.gui.save_error
        # Construct a valid 1x1 RGBA PNG from constants, never a desktop image.
        def chunk(kind, data):
            return (struct.pack(">I", len(data)) + kind + data
                    + struct.pack(">I", zlib.crc32(kind + data)))
        stream.write(b"\x89PNG\r\n\x1a\n"
                     + chunk(b"IHDR", struct.pack(">IIBBBBB", 1, 1, 8, 6, 0, 0, 0))
                     + chunk(b"IDAT", zlib.compress(b"\x00\x00\x00\x00\x00"))
                     + chunk(b"IEND", b""))


class GUI:
    FailSafeException = FailSafeException

    def __init__(self):
        self.FAILSAFE = True
        self.PAUSE = 0.1
        self.events = []
        self.down_error = None
        self.up_errors = []
        self.capture_error = None
        self.save_error = None
        self.after_capture = lambda: None

    def failSafeCheck(self):
        pass

    def keyDown(self, key):
        self.events.append(("down", key, self.FAILSAFE))
        if self.down_error:
            raise self.down_error

    def keyUp(self, key):
        self.events.append(("up", key, self.FAILSAFE))
        if self.up_errors:
            error = self.up_errors.pop(0)
            if error:
                raise error

    def screenshot(self, *, region):
        self.events.append(("capture", region, self.FAILSAFE))
        if self.capture_error:
            raise self.capture_error
        self.after_capture()
        return Image(self)


class DemoTests(unittest.TestCase):
    @contextmanager
    def simulation(self, duration=2, **options):
        wc = importlib.import_module("windows_control")
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GUI()
        report = {"actions": [], "evidence": []}
        config = wc.DemoOptions("w", **options)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            path = Path(directory) / "qa-test.json"
            with patch.object(wc, "verify_saved_png") as verify:
                control = wc.Demo(process, clock.now + duration, report, path, config,
                                  windows=windows, gui_loader=lambda: gui, clock=clock)
                yield control, process, clock, windows, gui, report, verify

    def execute_and_release(self, control):
        try:
            control.execute()
        finally:
            self.cleanup_result = control.release_keys()

    def test_single_key_then_saved_evidence_and_ordered_actions(self):
        with self.simulation() as (control, _, clock, windows, gui, report, verify):
            self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up", "capture"])
            self.assertEqual([a["name"] for a in report["actions"]],
                             ["setup", "window_wait", "key_down", "key_hold", "key_up", "capture_delay", "capture"])
            self.assertTrue(all(a["requested"] and a["attempted"] and a["executed"] for a in report["actions"]))
            self.assertTrue(all(a["status"] == "executed" for a in report["actions"]))
            evidence = report["evidence"][0]
            self.assertEqual(evidence["status"], "saved")
            self.assertEqual(evidence["hwnd"], 101)
            self.assertEqual(evidence["pid"], 1234)
            self.assertTrue(Path(evidence["path"]).is_file())
            verify.assert_called_once()
            self.assertEqual(report["window"]["hwnd"], 101)
            self.assertLess(clock.now, 2)
            self.assertFalse(windows.pixel_context)
            self.assertEqual(gui.PAUSE, 0.1)
            self.assertTrue(gui.FAILSAFE)
            self.assertEqual(self.cleanup_result, ([], False))

    def test_absent_or_pid_mismatch_timeout_without_input(self):
        for mismatch in (False, True):
            with self.subTest(mismatch=mismatch), self.simulation(window_wait_seconds=0.2) as values:
                control, _, clock, windows, gui, report, _ = values
                if mismatch:
                    windows.pid = 999
                else:
                    windows.handles = []
                with self.assertRaisesRegex(RuntimeError, "window|timeout"):
                    self.execute_and_release(control)
                self.assertLessEqual(clock.now, 0.2)
                self.assertEqual(gui.events, [])
                self.assertEqual(report["actions"][1]["status"], "error")
                self.assertEqual(report["actions"][-1]["status"], "skipped")

    def test_ambiguous_windows_are_rejected_without_guessing(self):
        with self.simulation() as (control, _, _, windows, gui, _, _):
            windows.handles = [101, 102]
            with self.assertRaisesRegex(RuntimeError, "ambiguous"):
                self.execute_and_release(control)
            self.assertEqual(gui.events, [])

    def test_wait_for_manual_focus_is_bounded_and_never_activates(self):
        with self.simulation(window_wait_seconds=0.2) as (control, _, clock, windows, gui, _, _):
            windows.focus = 999
            with self.assertRaisesRegex(RuntimeError, "focus|timeout"):
                self.execute_and_release(control)
            self.assertLessEqual(clock.now, 0.2)
            self.assertEqual(gui.events, [])

    def test_window_can_appear_and_user_can_focus_it_during_wait(self):
        with self.simulation() as (control, _, clock, windows, _, _, _):
            windows.handles = []
            original = clock.sleep
            def appear(seconds):
                original(seconds)
                windows.handles = [101]
            clock.sleep = appear
            self.execute_and_release(control)

    def test_wait_cannot_consume_hold_and_capture_budget(self):
        with self.simulation(duration=0.6, window_wait_seconds=10, hold_seconds=0.2,
                             capture_delay_seconds=0.2) as (control, _, clock, windows, gui, _, _):
            windows.handles = []
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            self.assertLessEqual(clock.now, 0.2 + 1e-9)
            self.assertEqual(gui.events, [])

    def test_window_wait_clock_crossing_timeout_before_sleep_uses_deadline_error(self):
        for crossed_time, error in ((0.21, "manual focus wait timeout"),
                                    (2.01, "observation deadline reached")):
            with self.subTest(crossed_time=crossed_time), self.simulation(window_wait_seconds=0.2) as values:
                control, _, _, windows, gui, _, _ = values
                windows.handles = []
                # End calculation, liveness and timeout checks pass; the sleep
                # calculation then observes time beyond the applicable deadline.
                clock = ScriptedClock([0, 0, 0, crossed_time])
                control.clock = clock
                with self.assertRaisesRegex(RuntimeError, error):
                    control.wait_window()
                self.assertEqual(clock.sleeps, [0])
                self.assertEqual(gui.events, [])

    def test_guarded_wait_clock_crossing_deadline_before_sleep_uses_deadline_error(self):
        with self.simulation() as (control, _, _, _, gui, _, _):
            control.hwnd, control.gui = 101, gui
            # End calculation and both liveness checks pass; remaining is
            # positive before the final clock read crosses the observation end.
            clock = ScriptedClock([0, 0, 0, 0.1, 2.01])
            control.clock = clock
            with self.assertRaisesRegex(RuntimeError, "observation deadline reached"):
                control.guarded_wait(0.2)
            self.assertEqual(clock.sleeps, [0])
            self.assertEqual(gui.events, [])

    def test_early_process_exit_during_wait(self):
        with self.simulation() as (control, process, clock, windows, gui, _, _):
            windows.handles = []
            def exit_process(seconds):
                clock.now += seconds
                process.returncode = 7
            clock.sleep = exit_process
            with self.assertRaisesRegex(RuntimeError, "exited"):
                self.execute_and_release(control)
            self.assertEqual(gui.events, [])

    def test_physical_key_and_modifiers_abort(self):
        for key in ("w", "shift", "ctrl", "alt", "winleft", "winright"):
            with self.subTest(key=key), self.simulation() as (control, _, _, windows, gui, _, _):
                windows.pressed = {key}
                with self.assertRaisesRegex(RuntimeError, "held"):
                    self.execute_and_release(control)
                self.assertEqual(gui.events, [])

    def test_wrong_focus_immediately_before_down(self):
        with self.simulation() as (control, _, _, windows, gui, _, _):
            original = windows.physically_held
            def change_focus(key):
                if key == "w":  # Initial demo-key check, after the window wait.
                    windows.focus = 999
                return original(key)
            windows.physically_held = change_focus
            with self.assertRaisesRegex(RuntimeError, "focus"):
                self.execute_and_release(control)
            self.assertEqual(gui.events, [])

    def test_focus_loss_while_held_uses_unsafe_emergency_release(self):
        with self.simulation() as (control, _, clock, windows, gui, report, _):
            def lose_focus(seconds):
                clock.now += seconds
                windows.focus = 999
            clock.sleep = lose_focus
            with self.assertRaisesRegex(RuntimeError, "focus"):
                self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])
            self.assertTrue(report["actions"][-1]["unsafe_global_release"])
            self.assertIn("risk", report["actions"][-1])
            self.assertEqual(control.held_keys, set())
            self.assertEqual(report["evidence"], [])

    def test_focus_loss_immediately_before_normal_up(self):
        with self.simulation() as (control, _, clock, windows, gui, report, _):
            original = control.guard
            def lose_on_up(*args, **kwargs):
                if next(a for a in report["actions"] if a["name"] == "key_up")["attempted"]:
                    windows.focus = 999
                return original(*args, **kwargs)
            control.guard = lose_on_up
            with self.assertRaisesRegex(RuntimeError, "focus"):
                self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])
            self.assertFalse(next(a for a in report["actions"] if a["name"] == "key_up")["executed"])
            self.assertTrue(report["actions"][-1]["unsafe_global_release"])

    def test_process_exit_invalid_handle_or_pid_change_while_held(self):
        for state in ("exit", "invalid", "pid"):
            with self.subTest(state=state), self.simulation() as values:
                control, process, clock, windows, gui, report, _ = values
                def change(seconds):
                    clock.now += seconds
                    if state == "exit":
                        process.returncode = 0
                    elif state == "invalid":
                        windows.valid = False
                    else:
                        windows.pid = 999
                clock.sleep = change
                with self.assertRaises(RuntimeError):
                    self.execute_and_release(control)
                self.assertEqual([e[0] for e in gui.events], ["down", "up"])
                self.assertTrue(report["actions"][-1]["unsafe_global_release"])

    def test_attempted_down_exception_still_releases(self):
        for error in (RuntimeError("down failed after injection"), KeyboardInterrupt(), FailSafeException("corner")):
            with self.subTest(error=type(error).__name__), self.simulation() as values:
                control, _, _, _, gui, report, _ = values
                gui.down_error = error
                with self.assertRaises(type(error)):
                    self.execute_and_release(control)
                self.assertEqual([e[0] for e in gui.events], ["down", "up"])
                action = next(a for a in report["actions"] if a["name"] == "key_down")
                self.assertTrue(action["attempted"])
                self.assertFalse(action["executed"])
                self.assertEqual(action["status"], "error")
                self.assertEqual(control.held_keys, set())

    def test_interrupt_during_hold_still_releases(self):
        with self.simulation() as (control, _, clock, _, gui, _, _):
            clock.sleep = lambda seconds: (_ for _ in ()).throw(KeyboardInterrupt())
            with self.assertRaises(KeyboardInterrupt):
                self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])

    def test_failsafe_blocked_normal_and_cleanup_up_bypasses_only_cleanup(self):
        with self.simulation() as (control, _, _, _, gui, report, _):
            gui.up_errors = [FailSafeException("normal"), FailSafeException("cleanup")]
            with self.assertRaises(FailSafeException):
                self.execute_and_release(control)
            self.assertEqual([e[2] for e in gui.events if e[0] == "up"], [True, True, False])
            self.assertTrue(gui.FAILSAFE)
            self.assertEqual(control.held_keys, set())
            self.assertTrue(report["actions"][-1]["failsafe_bypassed_for_release"])

    def test_failed_emergency_up_restores_failsafe_and_retains_held_marker(self):
        with self.simulation() as (control, _, _, _, gui, _, _):
            gui.down_error = RuntimeError("down")
            gui.up_errors = [FailSafeException("corner"), OSError("blocked"), OSError("blocked")]
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            errors, interrupted = self.cleanup_result
            self.assertTrue(errors)
            self.assertFalse(interrupted)
            self.assertEqual(control.held_keys, {"w"})
            self.assertTrue(gui.FAILSAFE)

    def test_repeated_cleanup_interruptions_retry_then_report_failure(self):
        with self.simulation() as (control, _, _, _, gui, report, _):
            gui.down_error = RuntimeError("down")
            gui.up_errors = [KeyboardInterrupt(), KeyboardInterrupt(), KeyboardInterrupt()]
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            self.assertEqual(len([e for e in gui.events if e[0] == "up"]), 3)
            self.assertTrue(self.cleanup_result[1])
            self.assertTrue(self.cleanup_result[0])
            self.assertEqual(control.held_keys, {"w"})
            self.assertEqual(report["actions"][-1]["status"], "error")

    def test_capture_and_save_errors_never_publish_evidence(self):
        for stage in ("capture", "save", "verify"):
            with self.subTest(stage=stage), self.simulation() as values:
                control, _, _, _, gui, report, verify = values
                if stage == "capture":
                    gui.capture_error = OSError("capture broke")
                elif stage == "save":
                    gui.save_error = OSError("save broke")
                else:
                    verify.side_effect = ValueError("not a valid PNG")
                with self.assertRaises((OSError, ValueError)):
                    self.execute_and_release(control)
                self.assertEqual(report["evidence"], [])
                self.assertEqual(report["actions"][-1]["status"], "error")

    def test_unsafe_state_change_during_screenshot_discards_image(self):
        for state in ("focus", "geometry", "pid", "invalid", "exit"):
            with self.subTest(state=state), self.simulation() as values:
                control, process, _, windows, gui, report, verify = values
                def change():
                    if state == "focus":
                        windows.focus = 999
                    elif state == "geometry":
                        windows.region = (20, 20, 320, 240)
                    elif state == "pid":
                        windows.pid = 999
                    elif state == "invalid":
                        windows.valid = False
                    else:
                        process.returncode = 0
                gui.after_capture = change
                with self.assertRaises(RuntimeError):
                    self.execute_and_release(control)
                self.assertEqual(report["evidence"], [])
                verify.assert_not_called()
                self.assertEqual(list(control.report_path.parent.glob("*.png")), [])

    def test_wrong_focus_before_capture_and_primary_monitor_rejection(self):
        for state in ("focus", "geometry"):
            with self.subTest(state=state), self.simulation() as values:
                control, _, clock, windows, gui, report, _ = values
                original = windows.window_region
                def change(hwnd):
                    if state == "focus":
                        windows.focus = 999
                    else:
                        windows.region = (-100, 0, 320, 240)
                    return original(hwnd)
                windows.window_region = change
                with self.assertRaises(RuntimeError):
                    self.execute_and_release(control)
                self.assertNotIn("capture", [e[0] for e in gui.events])
                self.assertEqual(report["evidence"], [])
                self.assertEqual(report["actions"][-1]["name"], "capture")
                self.assertEqual(report["actions"][-1]["status"], "error")

    def test_overrun_fails_instead_of_extending_observation(self):
        with self.simulation(duration=0.6, hold_seconds=0.2, capture_delay_seconds=0.2) as values:
            control, _, clock, _, gui, report, _ = values
            clock.sleep = lambda seconds: setattr(clock, "now", clock.now + 10)
            with self.assertRaisesRegex(RuntimeError, "deadline"):
                self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])
            self.assertEqual(report["evidence"], [])

    def test_missing_dependency_is_lazy_and_controlled(self):
        with self.simulation() as (control, _, _, _, gui, report, _):
            def unavailable():
                raise ImportError("PyAutoGUI missing")
            control.gui_loader = unavailable
            with self.assertRaisesRegex(RuntimeError, "PyAutoGUI"):
                self.execute_and_release(control)
            self.assertEqual(gui.events, [])
            self.assertEqual(report["actions"][0]["status"], "error")

    def test_state_change_during_save_marks_discarded_file_not_evidence(self):
        with self.simulation() as (control, _, _, windows, gui, report, _):
            original = Image.save
            def save_and_lose_focus(image, stream, format):
                original(image, stream, format)
                windows.focus = 999
            with patch.object(Image, "save", save_and_lose_focus):
                with self.assertRaisesRegex(RuntimeError, "focus"):
                    self.execute_and_release(control)
            self.assertEqual(report["evidence"], [])
            self.assertTrue(Path(report["actions"][-1]["discarded_evidence_path"]).exists())
            self.assertEqual(report["actions"][-1]["status"], "error")

    def test_screenshot_overrun_never_saves_evidence(self):
        with self.simulation() as (control, _, clock, _, gui, report, verify):
            gui.after_capture = lambda: setattr(clock, "now", 3)
            with self.assertRaisesRegex(RuntimeError, "deadline"):
                self.execute_and_release(control)
            self.assertEqual(report["evidence"], [])
            verify.assert_not_called()
            self.assertFalse(list(control.report_path.parent.glob("*.png")))

    def test_success_after_cleanup_interrupt_still_records_interrupt(self):
        with self.simulation() as (control, _, _, _, gui, report, _):
            gui.down_error = RuntimeError("down")
            gui.up_errors = [KeyboardInterrupt(), None]
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            self.assertTrue(self.cleanup_result[1])
            self.assertTrue(self.cleanup_result[0])
            self.assertEqual(control.held_keys, set())
            self.assertTrue(report["actions"][-1]["executed"])

    def test_dpi_setup_failure_is_recorded_before_any_gui_load(self):
        with self.simulation() as (control, _, _, windows, gui, report, _):
            @contextmanager
            def denied():
                raise RuntimeError("DPI unsupported")
                yield
            windows.physical_pixels = denied
            control.gui_loader = Mock(return_value=gui)
            with self.assertRaisesRegex(RuntimeError, "DPI"):
                self.execute_and_release(control)
            self.assertEqual(report["actions"][0]["status"], "error")
            control.gui_loader.assert_not_called()

    def test_corner_failsafe_is_checked_while_held(self):
        with self.simulation() as (control, _, clock, _, gui, report, _):
            def corner():
                if clock.now > 0:
                    raise FailSafeException("corner during hold")
            gui.failSafeCheck = corner
            with self.assertRaisesRegex(FailSafeException, "corner"):
                self.execute_and_release(control)
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])
            self.assertEqual(report["evidence"], [])

    def test_guard_failure_does_not_claim_global_input_attempt(self):
        with self.simulation() as (control, _, _, windows, _, report, _):
            windows.pressed = {"ctrl"}
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            down = next(a for a in report["actions"] if a["name"] == "key_down")
            self.assertFalse(down["global_input_attempted"])

    def test_unique_evidence_names(self):
        with self.simulation() as (control, _, _, _, _, report, _):
            self.execute_and_release(control)
            first = report["evidence"][0]["path"]
            control.execute()
            control.release_keys()
            second = report["evidence"][1]["path"]
            self.assertNotEqual(first, second)
            self.assertTrue(Path(first).is_file())
            self.assertTrue(Path(second).is_file())


class IntegrationTests(unittest.TestCase):
    def test_demo_report_and_cleanup_before_process_termination(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
        gui.down_error = KeyboardInterrupt()
        def terminated():
            self.assertEqual([e[0] for e in gui.events], ["down", "up"])
            process.returncode = -15
        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep), \
                    patch.object(process, "terminate", side_effect=terminated):
                report, path = runner.run(sys.executable, 2, directory,
                                          demo=wc.DemoOptions("w"), control_factory=factory)
            self.assertEqual(report["result"], "interrupted")
            self.assertEqual(report["functional_validation"], "not_performed")
            self.assertFalse(report["gameplay_validated"])
            self.assertEqual(report["evidence"], [])
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_cleanup_failure_propagates_and_cannot_claim_success(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
        gui.down_error = RuntimeError("down")
        gui.up_errors = [OSError("cannot release")] * 3
        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                report, path = runner.run(sys.executable, 2, directory,
                                          demo=wc.DemoOptions("w"), control_factory=factory)
            self.assertEqual(report["result"], "failure")
            self.assertIn("cannot release", report["error"])
            self.assertEqual(report["held_keys_unreleased"], ["w"])
            self.assertIsNotNone(process.returncode)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_no_demo_does_not_load_gui_or_win32(self):
        runner = importlib.import_module("runner")
        process, clock = FakeProcess(), Clock()
        original = __import__
        def guarded(name, *args, **kwargs):
            if name in ("pyautogui", "windows_control"):
                raise AssertionError("no-demo loaded desktop dependency")
            return original(name, *args, **kwargs)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep), \
                    patch("builtins.__import__", side_effect=guarded):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["actions"], [])
            self.assertEqual(report["evidence"], [])

    def test_all_demo_failures_persist_and_reap_root(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        for stage in ("dependency", "window", "focus", "input", "capture", "save", "validation"):
            with self.subTest(stage=stage):
                process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
                loader = Mock(return_value=gui)
                if stage == "dependency":
                    loader.side_effect = ImportError("missing PyAutoGUI")
                elif stage == "window":
                    windows.handles = [101, 102]
                elif stage == "focus":
                    windows.focus = 999
                elif stage == "input":
                    gui.down_error = OSError("input")
                elif stage == "capture":
                    gui.capture_error = OSError("capture")
                elif stage == "save":
                    gui.save_error = OSError("save")
                def factory(*args):
                    return wc.Demo(*args, windows=windows, gui_loader=loader, clock=clock)
                with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
                    with patch.object(runner.subprocess, "Popen", return_value=process), \
                            patch.object(runner.time, "monotonic", clock.monotonic), \
                            patch.object(runner.time, "sleep", clock.sleep), \
                            patch.object(wc, "verify_saved_png", side_effect=ValueError("validation") if stage == "validation" else None):
                        report, path = runner.run(sys.executable, 2, directory,
                                                  demo=wc.DemoOptions("w", window_wait_seconds=0.1),
                                                  control_factory=factory)
                    self.assertEqual(report["result"], "failure")
                    self.assertIn("error", report)
                    self.assertEqual(report["evidence"], [])
                    self.assertTrue(process.terminated)
                    self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)
                    self.assertTrue(any(a["status"] == "error" for a in report["actions"]))

    def test_success_keeps_original_observation_budget_and_timestamp(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            initial = []
            write = runner.write_report
            def capture_initial(stream, report):
                initial.append(report["date_utc"])
                write(stream, report)
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep), \
                    patch.object(runner, "write_report", side_effect=capture_initial), \
                    patch.object(wc, "verify_saved_png"):
                report, path = runner.run(sys.executable, 2, directory,
                                          demo=wc.DemoOptions("w"), control_factory=factory)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["observed_duration_seconds"], 2)
            self.assertEqual(clock.now, 2)
            self.assertEqual(initial, [report["date_utc"], report["date_utc"]])
            self.assertEqual(len(report["evidence"]), 1)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_real_loader_dependency_import_is_mocked(self):
        wc = importlib.import_module("windows_control")
        with patch.object(wc.importlib, "import_module", side_effect=ImportError("missing")) as load:
            with self.assertRaisesRegex(RuntimeError, "PyAutoGUI"):
                wc.load_gui()
            load.assert_called_once_with("pyautogui")

    def test_demo_duration_is_validated_before_any_launch_or_report(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen") as launch:
                with self.assertRaisesRegex(ValueError, "duration"):
                    runner.run(sys.executable, 0.4, directory, demo=wc.DemoOptions("w"))
                launch.assert_not_called()
                self.assertEqual(list(Path(directory).iterdir()), [])

    def test_cli_demo_validation_and_parameters(self):
        runner = importlib.import_module("runner")
        base = ["--exe", sys.executable, "--duration", "2"]
        cases = [["--demo-key", "ctrl"], ["--demo-key", "W"], ["--demo-key", "alt+tab"],
                 ["--demo-key", "w", "--key-hold", "nan"], ["--demo-key", "w", "--window-wait", "0"],
                 ["--demo-key", "w", "--capture-delay", "-1"], ["--key-hold", "0.1"],
                 ["--demo-key", "w", "--key-hold", "11"],
                 ["--demo-key", "w", "--capture-delay", "30"],
                 ["--demo-key", "w", "--window-wait", "61"]]
        for extra in cases:
            with self.subTest(extra=extra), patch("sys.stderr", new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as exc:
                    runner.main(base + extra)
                self.assertEqual(exc.exception.code, 2)
        with patch.object(runner, "run", return_value=({"result": "success"}, Path("r.json"))) as run, \
                patch("sys.stdout", new_callable=io.StringIO):
            self.assertEqual(runner.main(base + ["--demo-key", "w", "--key-hold", "0.1"]), 0)
            self.assertEqual(run.call_args.kwargs["demo"].hold_seconds, 0.1)


@unittest.skipUnless(sys.platform == "win32", "ctypes Windows ABI tests use a mocked DLL")
class ActionTimingTests(unittest.TestCase):
    """E3: action timestamps are seconds on the injected clock, relative to the launch origin."""

    simulation = DemoTests.simulation
    execute_and_release = DemoTests.execute_and_release

    def test_actions_record_clock_seconds_relative_to_launch_origin(self):
        with self.simulation() as (control, _, clock, _, gui, report, _):
            control.origin = -1.0  # launch began one second before the clock's zero
            original_down = gui.keyDown

            def slow_down(key):
                clock.now += 0.5
                original_down(key)

            gui.keyDown = slow_down
            self.execute_and_release(control)
            actions = {a["name"]: a for a in report["actions"]}
            self.assertEqual((actions["setup"]["started_at_seconds"], actions["setup"]["ended_at_seconds"]), (1.0, 1.0))
            self.assertEqual((actions["key_down"]["started_at_seconds"], actions["key_down"]["ended_at_seconds"]),
                             (1.0, 1.5))
            hold = actions["key_hold"]
            self.assertEqual(hold["started_at_seconds"], 1.5)
            self.assertAlmostEqual(hold["ended_at_seconds"], 1.7, places=6)
            previous_end = 0.0
            for action in report["actions"]:
                self.assertGreaterEqual(action["started_at_seconds"], previous_end)
                self.assertGreaterEqual(action["ended_at_seconds"], action["started_at_seconds"])
                previous_end = action["ended_at_seconds"]

    def test_failed_action_keeps_timing_and_later_actions_stay_none(self):
        with self.simulation() as (control, _, clock, _, gui, report, _):
            control.origin = 0.0
            gui.down_error = RuntimeError("down")
            with self.assertRaises(RuntimeError):
                self.execute_and_release(control)
            actions = {a["name"]: a for a in report["actions"]}
            self.assertEqual(actions["key_down"]["status"], "error")
            self.assertEqual(actions["key_down"]["started_at_seconds"], 0.0)
            self.assertEqual(actions["key_down"]["ended_at_seconds"], 0.0)
            for name in ("key_hold", "key_up", "capture_delay", "capture"):
                self.assertEqual(actions[name]["status"], "skipped")
                self.assertIsNone(actions[name]["started_at_seconds"])
                self.assertIsNone(actions[name]["ended_at_seconds"])
            cleanup = actions["key_release_cleanup"]
            self.assertEqual(cleanup["started_at_seconds"], 0.0)
            self.assertEqual(cleanup["ended_at_seconds"], 0.0)

    def test_requested_actions_start_with_unknown_times(self):
        wc = importlib.import_module("windows_control")
        for action in wc.requested_actions(wc.DemoOptions("w")):
            self.assertIsNone(action["started_at_seconds"])
            self.assertIsNone(action["ended_at_seconds"])

    def test_without_origin_timestamps_stay_none(self):
        with self.simulation() as (control, _, _, _, _, report, _):
            self.execute_and_release(control)
            for action in report["actions"]:
                self.assertEqual(action["status"], "executed")
                self.assertIsNone(action["started_at_seconds"])
                self.assertIsNone(action["ended_at_seconds"])

    def test_runner_passes_launch_origin_and_action_error_wins_first_failure(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
        clock.now = 50.0
        gui.down_error = RuntimeError("down")

        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)

        def launch(argv, **kwargs):
            clock.now += 0.25
            return process

        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", side_effect=launch),                     patch.object(runner.time, "monotonic", clock.monotonic),                     patch.object(runner.time, "sleep", clock.sleep):
                report, path = runner.run(sys.executable, 2, directory,
                                          demo=wc.DemoOptions("w"), control_factory=factory)
            actions = {a["name"]: a for a in report["actions"]}
            self.assertEqual(actions["setup"]["started_at_seconds"], 0.25)
            self.assertEqual(actions["key_down"]["status"], "error")
            observe = next(p for p in report["phases"] if p["name"] == "observe")
            self.assertEqual(observe["status"], "error")
            self.assertEqual(observe["started_at_seconds"], 0.25)
            self.assertEqual(report["first_failure"],
                             {"stage": "key_down", "error": "RuntimeError: down"})
            self.assertEqual(report["result"], "failure")
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_key_release_cleanup_error_does_not_outrank_the_original_failure(self):
        wc = importlib.import_module("windows_control")
        runner = importlib.import_module("runner")
        process, clock, gui, windows = FakeProcess(), Clock(), GUI(), Windows()
        gui.down_error = RuntimeError("down")
        gui.up_errors = [OSError("cannot release")] * 3

        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)

        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process),                     patch.object(runner.time, "monotonic", clock.monotonic),                     patch.object(runner.time, "sleep", clock.sleep):
                report, _ = runner.run(sys.executable, 2, directory,
                                       demo=wc.DemoOptions("w"), control_factory=factory)
            self.assertEqual(report["first_failure"]["stage"], "key_down")
            cleanup = next(p for p in report["phases"] if p["name"] == "cleanup")
            self.assertEqual(cleanup["status"], "error")


class Win32BoundaryTests(unittest.TestCase):
    def setUp(self):
        self.wc = importlib.import_module("windows_control")
        self.hwnd = 0x123456789
        self.pid = 1234
        self.handles = [self.hwnd]
        self.visible = True
        self.owner = 0
        self.root = self.hwnd
        self.iconic = False
        self.rect = (10, 20, 330, 260)
        def enum(callback, _):
            for hwnd in self.handles:
                if not callback(hwnd, 0):
                    return 0
            return 1
        def pid(hwnd, pointer):
            ctypes.cast(pointer, ctypes.POINTER(wintypes.DWORD)).contents.value = self.pid
            return 42
        def rect(hwnd, pointer):
            target = ctypes.cast(pointer, ctypes.POINTER(wintypes.RECT)).contents
            target.left, target.top, target.right, target.bottom = self.rect
            return 1
        self.api = SimpleNamespace(
            EnumWindows=Mock(side_effect=enum), GetWindowThreadProcessId=Mock(side_effect=pid),
            IsWindow=Mock(return_value=True), IsWindowVisible=Mock(side_effect=lambda h: self.visible),
            IsIconic=Mock(side_effect=lambda h: self.iconic),
            GetAncestor=Mock(side_effect=lambda h, flag: self.root),
            GetWindow=Mock(side_effect=lambda h, flag: self.owner),
            GetForegroundWindow=Mock(return_value=self.hwnd), GetWindowRect=Mock(side_effect=rect),
            GetSystemMetrics=Mock(side_effect=lambda index: (1920, 1080)[index]),
            GetAsyncKeyState=Mock(return_value=0), SetThreadDpiAwarenessContext=Mock(return_value=0x12345678))
        self.windows = self.wc.Win32(user32=self.api)

    def test_signatures_and_callback_preserve_pointer_sized_hwnd(self):
        self.assertEqual(self.windows.owned_windows(1234), [self.hwnd])
        self.assertEqual(self.windows.foreground(), self.hwnd)
        self.assertEqual(self.api.GetAncestor.restype, wintypes.HWND)
        self.assertEqual(self.api.GetWindow.restype, wintypes.HWND)
        self.assertEqual(self.api.GetForegroundWindow.restype, wintypes.HWND)
        self.assertEqual(self.api.SetThreadDpiAwarenessContext.restype, ctypes.c_void_p)
        for function in vars(self.api).values():
            self.assertIsNotNone(function.argtypes)
            self.assertIsNotNone(function.restype)
        self.windows.validate(self.hwnd, self.pid)

    def test_owned_pid_visible_root_non_dialog_non_minimized_only(self):
        for state in ("pid", "visible", "root", "owner", "iconic"):
            with self.subTest(state=state):
                previous = getattr(self, state)
                setattr(self, state, {"pid": 999, "visible": False, "root": 999,
                                     "owner": 999, "iconic": True}[state])
                self.assertEqual(self.windows.owned_windows(1234), [])
                setattr(self, state, previous)
        self.api.IsWindow.return_value = False
        with self.assertRaises(RuntimeError):
            self.windows.validate(self.hwnd, self.pid)

    def test_enum_and_pid_api_errors_are_controlled(self):
        self.api.EnumWindows.side_effect = None
        self.api.EnumWindows.return_value = 0
        with self.assertRaisesRegex(RuntimeError, "EnumWindows"):
            self.windows.owned_windows(self.pid)
        self.api.GetWindowThreadProcessId.side_effect = None
        self.api.GetWindowThreadProcessId.return_value = 0
        with self.assertRaisesRegex(RuntimeError, "ProcessId"):
            self.windows.validate(self.hwnd, self.pid)

    def test_callback_exception_propagates_not_swallowed_by_ctypes(self):
        self.api.IsWindow.side_effect = KeyboardInterrupt
        with self.assertRaises(KeyboardInterrupt):
            self.windows.owned_windows(self.pid)

    def test_rect_primary_monitor_bounds_and_api_failure(self):
        self.assertEqual(self.windows.window_region(self.hwnd), (10, 20, 320, 240))
        for rect in ((-1, 0, 10, 10), (0, 0, 2000, 100), (0, 0, 100, 1200), (0, 0, 0, 10)):
            self.rect = rect
            with self.assertRaisesRegex(RuntimeError, "primary"):
                self.windows.window_region(self.hwnd)
        self.api.GetWindowRect.side_effect = None
        self.api.GetWindowRect.return_value = 0
        with self.assertRaisesRegex(RuntimeError, "GetWindowRect"):
            self.windows.window_region(self.hwnd)

    def test_dpi_context_restored_after_exception(self):
        with self.assertRaisesRegex(RuntimeError, "body"):
            with self.windows.physical_pixels():
                raise RuntimeError("body")
        calls = self.api.SetThreadDpiAwarenessContext.call_args_list
        self.assertEqual(calls[0].args[0].value, ctypes.c_void_p(-4).value)
        self.assertEqual(calls[1].args[0], 0x12345678)
        self.api.SetThreadDpiAwarenessContext.return_value = 0
        with self.assertRaisesRegex(RuntimeError, "DPI"):
            with self.windows.physical_pixels():
                self.fail("should not enter")

    def test_async_key_state_checks_high_bit_not_prior_press_low_bit(self):
        self.api.GetAsyncKeyState.return_value = 1
        self.assertFalse(self.windows.physically_held("w"))
        self.api.GetAsyncKeyState.return_value = -32768
        self.assertTrue(self.windows.physically_held("ctrl"))
        self.api.GetAsyncKeyState.assert_called_with(0x11)


@unittest.skipUnless(importlib.util.find_spec("PIL"), "PNG decode checks require demo Pillow dependency")
class SavedPNGTests(unittest.TestCase):
    def test_saved_fixture_is_valid_and_dimensions_are_checked(self):
        wc = importlib.import_module("windows_control")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            path = Path(directory) / "fixture.png"
            with path.open("xb") as stream:
                Image(GUI()).save(stream, "PNG")
            wc.verify_saved_png(path, (0, 0, 1, 1))
            with self.assertRaisesRegex(ValueError, "dimensions"):
                wc.verify_saved_png(path, (0, 0, 2, 2))

    def test_invalid_or_truncated_png_rejected(self):
        wc = importlib.import_module("windows_control")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            for index, payload in enumerate((b"not PNG", b"\x89PNG\r\n\x1a\n")):
                path = Path(directory) / f"bad-{index}.png"
                path.write_bytes(payload)
                with self.assertRaises((OSError, ValueError, SyntaxError)):
                    wc.verify_saved_png(path, (0, 0, 1, 1))

    def test_full_demo_with_real_png_validation_but_fake_capture(self):
        wc = importlib.import_module("windows_control")
        windows = Windows()
        windows.region = (10, 20, 1, 1)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            report = {"actions": [], "evidence": []}
            control = wc.Demo(FakeProcess(), 2, report, Path(directory) / "qa-fixture.json",
                              wc.DemoOptions("w"), windows=windows, gui_loader=GUI, clock=Clock())
            try:
                control.execute()
            finally:
                control.release_keys()
            self.assertEqual(len(report["evidence"]), 1)


if __name__ == "__main__":
    unittest.main()
