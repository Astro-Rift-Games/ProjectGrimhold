"""QA003 offline images and mocked boundaries only; never import PyAutoGUI."""
from contextlib import contextmanager
import importlib
import importlib.util
import io
import json
from pathlib import Path
import random
import sys
import tempfile
import unittest
from unittest.mock import Mock, patch

from test_runner import QA_ROOT, Clock, FakeProcess
from test_windows_control import GUI, Windows
import test_windows_control
import readiness
import runner
import windows_control as wc

REFERENCE = QA_ROOT / "references/main-menu-iniciar.png"
HAS_PILLOW = importlib.util.find_spec("PIL") is not None


@unittest.skipUnless(HAS_PILLOW, "readiness requires optional Pillow")
class DetectorTests(unittest.TestCase):
    def setUp(self):
        from PIL import Image
        self.Image = Image
        self.detector = readiness.Detector(REFERENCE)

    def frame(self, location=(107, 203), size=(640, 480)):
        frame = self.Image.new("RGB", size, "black")
        frame.paste(self.detector.reference, location)
        return frame

    def match(self, frame):
        return self.detector.match(frame, check=lambda: None)

    def test_translated_menu_reference_matches_but_black_does_not(self):
        self.assertEqual(self.match(self.frame())["location"], [107, 203])
        self.assertIsNone(self.match(self.Image.new("RGB", (640, 480)))["location"])

    def test_small_rgb_and_antialias_perturbations_match(self):
        frame = self.frame()
        randomizer = random.Random(123)
        for y in range(203, 233):
            for x in range(107, 195):
                frame.putpixel((x, y), tuple(max(0, min(255, c + randomizer.randint(-3, 3)))
                                             for c in frame.getpixel((x, y))))
        result = self.match(frame)
        self.assertEqual(result["location"], [107, 203])
        self.assertGreater(result["score"], 0)
        self.assertLess(result["score"], 4)

    def test_flat_noise_wrong_text_and_near_match_do_not_pass(self):
        from PIL import ImageDraw
        randomizer = random.Random(42)
        noise = self.Image.frombytes("RGB", (320, 240), randomizer.randbytes(320 * 240 * 3))
        wrong = self.Image.new("RGB", (320, 240), (29, 29, 35))
        ImageDraw.Draw(wrong).text((80, 90), "OPCIONES", fill="white")
        near = self.frame()
        # Damage a glyph away from the anchor row: background-dominated mean is insufficient.
        ImageDraw.Draw(near).rectangle((119, 211, 126, 214), fill=(29, 29, 35))
        for frame in (self.Image.new("RGB", (320, 240), "white"),
                      self.Image.new("RGB", (320, 240), (29, 29, 35)), noise, wrong, near):
            with self.subTest(frame=frame):
                self.assertIsNone(self.match(frame)["location"])

    def test_duplicate_matches_are_ambiguous_not_ready(self):
        frame = self.frame()
        frame.paste(self.detector.reference, (300, 203))
        result = self.match(frame)
        self.assertIsNone(result["location"])
        self.assertTrue(result["ambiguous"])

    def test_scale_mismatch_is_not_searched(self):
        frame = self.Image.new("RGB", (640, 480))
        frame.paste(self.detector.reference.resize((176, 60)), (100, 100))
        self.assertIsNone(self.match(frame)["location"])

    def test_missing_corrupt_blank_transparent_small_and_oversized_reference_rejected(self):
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            path = Path(directory) / "invalid.png"
            with self.assertRaises(OSError):
                readiness.Detector(path)
            path.write_bytes(b"not PNG")
            with self.assertRaises(OSError):
                readiness.Detector(path)
            for size, mode, color in (((88, 30), "RGB", "black"), ((8, 8), "RGB", "white"),
                                       ((257, 30), "RGB", "black"), ((88, 30), "RGBA", (0, 0, 0, 0))):
                self.Image.new(mode, size, color).save(path)
                with self.assertRaises(ValueError):
                    readiness.Detector(path)
            path.write_bytes(b"x" * (1024 * 1024 + 1))
            with self.assertRaisesRegex(ValueError, "MiB"):
                readiness.Detector(path)

    def test_frame_size_and_candidate_budgets_are_bounded(self):
        for size in ((1921, 1080), (1920, 1081), (32, 10)):
            with self.subTest(size=size), self.assertRaises(ValueError):
                self.match(self.Image.new("RGB", size))
        # Repeat the anchor alone, not a complete template: force search-budget exhaustion.
        mask = self.Image.new("L", (400, 240))
        row = self.Image.frombytes("L", (88, 1), self.detector.anchor)
        for y in range(240):
            mask.paste(row, (10, y))
        with patch.object(readiness, "MAX_CANDIDATES", 5):
            with self.assertRaisesRegex(ValueError, "candidate budget"):
                self.match(mask.convert("RGB"))

    def test_processing_checks_deadline_repeatedly(self):
        checks = Mock(side_effect=[None, None, None, RuntimeError("deadline")])
        with self.assertRaisesRegex(RuntimeError, "deadline"):
            self.detector.match(self.Image.new("RGB", (1920, 1080)), check=checks)
        self.assertEqual(checks.call_count, 4)


class OptionsTests(unittest.TestCase):
    def test_finite_bounded_options_and_required_consistency(self):
        for field, values in (("timeout_seconds", (0, 61, float("nan"), float("inf"))),
                              ("interval_seconds", (0, 0.01, 6, float("nan"))),
                              ("tolerance", (0, 21, float("inf"), "bad")),
                              ("consistent_count", (1, 11, 2.5, True))):
            for value in values:
                with self.subTest(field=field, value=value), self.assertRaises(ValueError):
                    readiness.ReadyOptions(REFERENCE, **{field: value})
        self.assertEqual(readiness.ReadyOptions(REFERENCE).consistent_count, 3)
        with self.assertRaises(ValueError):
            wc.DemoOptions("w", readiness=object())

    def test_cli_requires_explicit_reference_and_demo(self):
        base = ["--exe", sys.executable, "--duration", "2"]
        for extra in (["--ready-reference", str(REFERENCE)], ["--demo-key", "w", "--ready-timeout", "1"],
                      ["--ready-interval", "0.2"], ["--demo-key", "w", "--ready-reference", str(REFERENCE),
                                                "--ready-tolerance", "nan"]):
            with self.subTest(extra=extra), patch("sys.stderr", new_callable=io.StringIO):
                with self.assertRaises(SystemExit) as error:
                    runner.main(base + extra)
                self.assertEqual(error.exception.code, 2)
        with patch.object(runner, "run", return_value=({"result": "success"}, Path("r.json"))) as run, \
                patch("sys.stdout", new_callable=io.StringIO):
            runner.main(base + ["--demo-key", "w", "--ready-reference", str(REFERENCE), "--ready-count", "2"])
            self.assertEqual(run.call_args.kwargs["demo"].readiness.consistent_count, 2)

    def test_initial_report_unrequested_only_is_not_checked(self):
        self.assertEqual(readiness.initial_report()["state"], "not_checked")
        self.assertEqual(readiness.initial_report(readiness.ReadyOptions(REFERENCE))["state"], "blocked")


@unittest.skipUnless(HAS_PILLOW, "readiness requires optional Pillow")
class ReadinessLifecycleTests(unittest.TestCase):
    @contextmanager
    def simulation(self, *, duration=2, timeout=0.6, count=3, reference=REFERENCE):
        from PIL import Image
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GUI()
        windows.client_region = lambda hwnd: windows.window_region(hwnd)
        report = {"actions": [], "evidence": []}
        options = wc.DemoOptions("w", hold_seconds=0.1, capture_delay_seconds=0.1,
                                readiness=readiness.ReadyOptions(reference, timeout_seconds=timeout,
                                                                interval_seconds=0.1, consistent_count=count))
        frame = Image.new("RGB", windows.region[2:])
        with Image.open(REFERENCE) as template:
            frame.paste(template, (70, 90))
        def capture(*, region):
            gui.events.append(("capture", region, gui.FAILSAFE))
            if gui.capture_error:
                raise gui.capture_error
            gui.after_capture()
            return frame.copy()
        gui.screenshot = capture
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            control = wc.Demo(process, duration, report, Path(directory) / "qa-test.json", options,
                              windows=windows, gui_loader=lambda: gui, clock=clock)
            yield control, process, clock, windows, gui, report, frame

    def execute(self, control):
        try:
            control.execute()
        finally:
            errors, interrupted = control.release_keys()
            self.assertEqual(errors, [])
            self.assertFalse(interrupted)

    def assert_no_input(self, gui):
        self.assertFalse(any(event[0] in ("down", "up") for event in gui.events))

    def test_stable_ready_strictly_precedes_keys_and_preserves_cleanup(self):
        with self.simulation() as (control, _, clock, windows, gui, report, _):
            self.execute(control)
            self.assertEqual([e[0] for e in gui.events], ["capture"] * 3 + ["down", "up", "capture"])
            self.assertEqual([a["name"] for a in report["actions"]][:4],
                             ["setup", "window_wait", "readiness", "key_down"])
            ready = report["readiness"]
            self.assertEqual((ready["state"], ready["attempts"], ready["streak"]), ("ready", 3, 3))
            self.assertEqual(ready["location"], [70, 90])
            self.assertEqual(len(ready["reference_sha256"]), 64)
            self.assertAlmostEqual(ready["time_to_ready_seconds"], 0.2)
            self.assertFalse(control.held_keys)

    def test_flicker_location_and_safe_geometry_changes_reset_streak(self):
        from PIL import Image
        for case in ("flicker", "location", "geometry"):
            with self.subTest(case=case), self.simulation(timeout=1.0) as values:
                control, _, _, windows, gui, report, frame = values
                original = gui.screenshot
                calls = 0
                def capture(*, region):
                    nonlocal calls
                    calls += 1
                    if calls == 2:
                        if case == "flicker":
                            return Image.new("RGB", region[2:])
                        if case == "location":
                            image = Image.new("RGB", region[2:])
                            image.paste(frame.crop((70, 90, 158, 120)), (80, 90))
                            return image
                    return original(region=region)
                gui.screenshot = capture
                if case == "geometry":
                    original_sleep = control.clock.sleep
                    def move(seconds):
                        original_sleep(seconds)
                        windows.region = (20 if calls == 1 else 10, 20, 320, 240)
                    control.clock.sleep = move
                self.execute(control)
                self.assertEqual(report["readiness"]["attempts"], 5)

    def test_timeout_saves_unique_valid_historical_diagnostic_without_input(self):
        from PIL import Image
        with self.simulation(timeout=0.3) as (control, _, _, _, gui, report, frame):
            frame.paste("black", (0, 0, *frame.size))
            for _ in range(2):
                with self.assertRaisesRegex(RuntimeError, "readiness timeout"):
                    self.execute(control)
            self.assert_no_input(gui)
            self.assertEqual(report["readiness"]["state"], "blocked")
            self.assertEqual(report["readiness"]["streak"], 0)
            self.assertEqual(len(report["evidence"]), 2)
            self.assertNotEqual(report["evidence"][0]["path"], report["evidence"][1]["path"])
            for evidence in report["evidence"]:
                self.assertEqual(evidence["purpose"], "readiness_diagnostic")
                self.assertTrue(evidence["stale"])
                self.assertEqual(evidence["role"], "previous_guarded_frame_not_current_authority")
                wc.verify_saved_png(Path(evidence["path"]), evidence["region"])
                with Image.open(evidence["path"]) as image:
                    self.assertEqual(image.getextrema(), ((0, 0),) * 3)

    def test_focus_exit_and_interrupt_preserve_only_previous_guarded_frame(self):
        for state in ("focus", "exit", "interrupt"):
            with self.subTest(state=state), self.simulation() as values:
                control, process, clock, windows, gui, report, _ = values
                def change(seconds):
                    clock.now += seconds
                    if state == "focus":
                        windows.focus = 999
                    elif state == "exit":
                        process.returncode = 7
                    else:
                        raise KeyboardInterrupt()
                clock.sleep = change
                with self.assertRaises((RuntimeError, KeyboardInterrupt)):
                    self.execute(control)
                self.assert_no_input(gui)
                self.assertEqual(len(gui.events), 1)
                self.assertEqual(report["readiness"]["state"], "blocked")
                self.assertTrue(report["evidence"][0]["stale"])

    def test_unsafe_capture_and_missing_image_are_never_saved(self):
        for state in ("focus", "geometry", "exit", "none", "size", "capture"):
            with self.subTest(state=state), self.simulation() as values:
                control, process, _, windows, gui, report, _ = values
                if state == "none":
                    gui.screenshot = lambda **kwargs: None
                elif state == "size":
                    from PIL import Image
                    gui.screenshot = lambda **kwargs: Image.new("RGB", (1, 1))
                elif state == "capture":
                    gui.capture_error = OSError("capture failed")
                else:
                    def change():
                        if state == "focus":
                            windows.focus = 999
                        elif state == "geometry":
                            windows.region = (20, 20, 320, 240)
                        else:
                            process.returncode = 0
                    gui.after_capture = change
                with self.assertRaises((RuntimeError, AttributeError, ValueError, OSError)):
                    self.execute(control)
                self.assert_no_input(gui)
                self.assertEqual(report["evidence"], [])
                self.assertIn("No previously", report["readiness"]["evidence_error"])

    def test_detector_and_diagnostic_errors_remain_distinct(self):
        with self.simulation() as (control, _, _, _, gui, report, _):
            with patch.object(readiness.Detector, "match", side_effect=ValueError("detector broke")), \
                    patch.object(wc, "verify_saved_png", side_effect=OSError("diagnostic broke")):
                with self.assertRaisesRegex(ValueError, "detector broke"):
                    self.execute(control)
            self.assert_no_input(gui)
            self.assertIn("detector broke", report["readiness"]["error"])
            self.assertIn("diagnostic broke", report["readiness"]["evidence_error"])
            self.assertTrue(Path(report["readiness"]["discarded_evidence_path"]).exists())
            self.assertEqual(report["evidence"], [])

    def test_missing_reference_setup_window_focus_and_dependency_failures_block(self):
        for stage in ("reference", "window", "focus", "dependency", "dpi"):
            with self.subTest(stage=stage), self.simulation() as values:
                control, _, _, windows, gui, report, _ = values
                if stage == "reference":
                    object.__setattr__(control.options.readiness, "reference", REFERENCE.parent / "missing.png")
                elif stage == "window":
                    windows.handles = [101, 102]
                elif stage == "focus":
                    windows.focus = 999
                elif stage == "dependency":
                    control.gui_loader = Mock(side_effect=ImportError("Pillow/PyAutoGUI unavailable"))
                else:
                    @contextmanager
                    def denied():
                        raise RuntimeError("DPI unavailable")
                        yield
                    windows.physical_pixels = denied
                with self.assertRaises((RuntimeError, OSError)):
                    self.execute(control)
                self.assert_no_input(gui)
                self.assertEqual(report["readiness"]["state"], "blocked")
                self.assertNotEqual(report["readiness"]["error"], "Readiness has not completed.")
                self.assertEqual(report["evidence"], [])

    def test_processing_and_window_wait_share_original_deadline(self):
        with self.simulation(duration=0.5, timeout=10) as values:
            control, _, clock, windows, gui, report, _ = values
            windows.handles = []
            sleep = clock.sleep
            def appear(seconds):
                sleep(seconds)
                if clock.now >= 0.2:
                    windows.handles = [101]
            clock.sleep = appear
            # Only ~0.08s readiness budget remains, not a newly started 10s observation.
            with self.assertRaisesRegex(RuntimeError, "readiness timeout"):
                self.execute(control)
            self.assertLessEqual(clock.now, 0.3 + 1e-9)
            self.assert_no_input(gui)
        with self.simulation(duration=0.5) as (control, _, clock, _, gui, report, _):
            def slow_match(detector, image, *, check):
                clock.now = 0.51
                check()
            with patch.object(readiness.Detector, "match", slow_match):
                with self.assertRaisesRegex(RuntimeError, "observation deadline"):
                    self.execute(control)
            self.assert_no_input(gui)
            self.assertEqual(report["evidence"], [])
            self.assertIn("Global observation deadline", report["readiness"]["evidence_error"])

    def test_guard_checks_geometry_during_processing(self):
        with self.simulation() as (control, _, _, windows, gui, report, _):
            def match(detector, image, *, check):
                windows.region = (20, 20, 320, 240)
                check()
            with patch.object(readiness.Detector, "match", match):
                with self.assertRaisesRegex(RuntimeError, "geometry changed"):
                    self.execute(control)
            self.assert_no_input(gui)
            self.assertTrue(report["evidence"][0]["stale"])

    def test_client_only_not_whole_window_capture_and_prebound_size(self):
        with self.simulation() as (control, _, _, windows, gui, report, frame):
            windows.client_region = lambda hwnd: (30, 40, 320, 240)
            self.execute(control)
            self.assertEqual(gui.events[0][1], (30, 40, 320, 240))
            self.assertEqual(gui.events[-1][1], windows.region)
        with self.simulation() as (control, _, _, windows, gui, report, _):
            windows.client_region = lambda hwnd: (0, 0, 1921, 1080)
            with self.assertRaisesRegex(RuntimeError, "bounded"):
                self.execute(control)
            self.assertEqual(gui.events, [])

    def test_runner_ready_and_timeout_reports_keep_lifecycle_independent(self):
        for blocked in (False, True):
            with self.subTest(blocked=blocked), self.simulation(timeout=0.3) as values:
                control, process, clock, _, gui, _, frame = values
                if blocked:
                    frame.paste("black", (0, 0, *frame.size))
                def factory(process, deadline, report, path, options):
                    control.report, control.report_path, control.deadline = report, path, deadline
                    return control
                with patch.object(runner.subprocess, "Popen", return_value=process), \
                        patch.object(runner.time, "monotonic", clock.monotonic), \
                        patch.object(runner.time, "sleep", clock.sleep):
                    report, path = runner.run(sys.executable, 2, control.report_path.parent,
                                              demo=control.options, control_factory=factory)
                self.assertEqual(report["result"], "failure" if blocked else "success")
                self.assertEqual(report["readiness"]["state"], "blocked" if blocked else "ready")
                self.assertTrue(process.terminated)
                self.assertFalse(report["gameplay_validated"])
                self.assertEqual(report["functional_validation"], "not_performed")
                self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)
                if blocked:
                    self.assert_no_input(gui)
                    self.assertTrue(report["readiness"]["error"].startswith("RuntimeError: readiness timeout"))
                else:
                    self.assertEqual(clock.now, 2)

    def test_corrupt_and_blank_references_block_with_no_input(self):
        from PIL import Image
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            path = Path(directory) / "bad.png"
            for corrupt in (True, False):
                if corrupt:
                    path.write_bytes(b"bad PNG")
                else:
                    Image.new("RGB", (88, 30)).save(path)
                with self.subTest(corrupt=corrupt), self.simulation(reference=path) as values:
                    control, _, _, _, gui, report, _ = values
                    with self.assertRaises((ValueError, OSError)):
                        self.execute(control)
                    self.assert_no_input(gui)
                    self.assertEqual(report["readiness"]["state"], "blocked")
                    self.assertEqual(report["readiness"]["attempts"], 0)

    def test_readiness_screenshot_global_overrun_discards_frame(self):
        with self.simulation(duration=0.5) as (control, _, clock, _, gui, report, _):
            gui.after_capture = lambda: setattr(clock, "now", 0.6)
            with self.assertRaisesRegex(RuntimeError, "observation deadline"):
                self.execute(control)
            self.assert_no_input(gui)
            self.assertEqual(report["evidence"], [])
            self.assertIn("No previously", report["readiness"]["evidence_error"])

    def test_diagnostic_save_failure_does_not_replace_detector_error(self):
        from PIL import Image
        with self.simulation() as (control, _, _, _, gui, report, _):
            with patch.object(readiness.Detector, "match", side_effect=ValueError("detector broke")), \
                    patch.object(Image.Image, "save", side_effect=OSError("save denied")):
                with self.assertRaisesRegex(ValueError, "detector broke"):
                    self.execute(control)
            self.assert_no_input(gui)
            self.assertIn("save denied", report["readiness"]["evidence_error"])
            self.assertEqual(report["evidence"], [])

    def test_launch_failure_has_blocked_independent_readiness_report(self):
        options = wc.DemoOptions("w", readiness=readiness.ReadyOptions(REFERENCE))
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", side_effect=OSError("launch denied")):
                report, path = runner.run(sys.executable, 2, directory, demo=options)
            self.assertEqual(report["readiness"]["state"], "blocked")
            self.assertIn("launch denied", report["readiness"]["error"])
            self.assertFalse(report["gameplay_validated"])
            self.assertEqual(report["functional_validation"], "not_performed")
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_default_mode_reports_not_checked_without_pillow_import(self):
        process, clock = FakeProcess(), Clock()
        original = __import__
        def safe_import(name, *args, **kwargs):
            if name.startswith("PIL") or name == "pyautogui":
                raise AssertionError("default mode imported optional dependency")
            return original(name, *args, **kwargs)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep), \
                    patch("builtins.__import__", side_effect=safe_import):
                report, _ = runner.run(sys.executable, 0.5, directory)
            self.assertEqual(report["readiness"]["state"], "not_checked")
            self.assertEqual(report["actions"], [])


@unittest.skipUnless(sys.platform == "win32", "mocked Windows ABI")
class ClientRegionTests(unittest.TestCase):
    def test_client_measurement_uses_pointer_sized_hwnd_and_rejects_outside(self):
        import ctypes
        from ctypes import wintypes
        boundary = test_windows_control.Win32BoundaryTests()
        boundary.setUp()
        def rect(hwnd, pointer):
            value = ctypes.cast(pointer, ctypes.POINTER(wintypes.RECT)).contents
            value.left, value.top, value.right, value.bottom = 0, 0, 300, 200
            return 1
        def origin(hwnd, pointer):
            value = ctypes.cast(pointer, ctypes.POINTER(wintypes.POINT)).contents
            value.x, value.y = 20, 40
            return 1
        boundary.api.GetClientRect = Mock(side_effect=rect)
        boundary.api.ClientToScreen = Mock(side_effect=origin)
        self.assertEqual(boundary.windows.client_region(boundary.hwnd), (20, 40, 300, 200))
        self.assertEqual(boundary.api.ClientToScreen.argtypes[0], wintypes.HWND)
        boundary.rect = (10, 20, 100, 100)
        with self.assertRaisesRegex(RuntimeError, "client area outside"):
            boundary.windows.client_region(boundary.hwnd)
        boundary.api.ClientToScreen.side_effect = None
        boundary.api.ClientToScreen.return_value = 0
        with self.assertRaisesRegex(RuntimeError, "cannot measure"):
            boundary.windows.client_region(boundary.hwnd)


if __name__ == "__main__":
    unittest.main()
