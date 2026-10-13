"""E4 login-gate: fake Win32/GUI, synthetic frames and temp logs only; no desktop or real input."""
from contextlib import contextmanager
import importlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

from test_runner import QA_ROOT, Clock, FakeProcess
from test_windows_control import GUI, ScanKeyboard, Windows
import login_gate
import readiness
import windows_control as wc

REFERENCE = QA_ROOT / "references/main-menu-iniciar.png"
HAS_PILLOW = importlib.util.find_spec("PIL") is not None
SHOWN = "[MainMenuController] Login panel shown."
CLOSED = "[MainMenuController] Login panel closed."


class Game:
    """Tiny stand-in for the menu: screen + Player log side effects of each key."""
    def __init__(self, log_path):
        self.screen, self.log_path = "menu", log_path
        self.hides_menu, self.logs_shown, self.logs_closed = True, True, True

    def write(self, line):
        with open(self.log_path, "ab") as stream:
            stream.write(line.encode("utf-8") + b"\n")

    def press(self, key):
        if self.screen == "menu" and key == "enter":
            if self.logs_shown:
                self.write(SHOWN)
            if self.hides_menu:
                self.screen = "login"
        elif self.screen == "login" and key == "escape":
            if self.logs_closed:
                self.write(CLOSED)
            self.screen = "menu"


class GameGUI(GUI):
    game = None
    on_down = None

    def keyDown(self, key):
        super().keyDown(key)
        self.game.press(key)
        if self.on_down:
            self.on_down(key)

    def keys(self, kind):
        return [event[1] for event in self.events if event[0] == kind]


class GUIKeyboard(ScanKeyboard):
    """Scan-code keyboard double that logs into the GUI event list so key assertions stay in one place."""
    def __init__(self, gui):
        super().__init__()
        self.gui = gui

    def key_down(self, key):
        scan = super().key_down(key)
        self.gui.keyDown(key)
        return scan

    def key_up(self, key):
        scan = super().key_up(key)
        self.gui.keyUp(key)
        return scan


@unittest.skipUnless(HAS_PILLOW, "login-gate requires optional Pillow")
class LoginGateFlowTests(unittest.TestCase):
    @contextmanager
    def simulation(self, *, control_mode=None, duration=300, log_exists=True, start_screen="menu"):
        from PIL import Image
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GameGUI()
        windows.client_region = lambda hwnd: windows.window_region(hwnd)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            log_path = Path(directory) / "player.log"
            if log_exists:
                log_path.write_bytes(b"startup noise\n")
            game = Game(log_path)
            game.screen = start_screen
            gui.game = game
            with Image.open(REFERENCE) as template:
                menu = Image.new("RGB", windows.region[2:])
                menu.paste(template, (70, 90))
            blank = Image.new("RGB", windows.region[2:])

            def capture(*, region):
                gui.events.append(("capture", region, gui.FAILSAFE))
                return (menu if game.screen == "menu" else blank).copy()
            gui.screenshot = capture
            report = {"actions": [], "evidence": []}
            options = wc.DemoOptions("w", hold_seconds=0.1, capture_delay_seconds=0.1)
            control = wc.Demo(process, clock.now + duration, report, Path(directory) / "qa-test.json",
                              options, windows=windows, gui_loader=lambda: gui, clock=clock, origin=0.0,
                              keyboard=GUIKeyboard(gui))
            control.gui, control.hwnd = gui, 101
            flow = login_gate.LoginGateFlow(
                control, readiness.Detector(REFERENCE), login_gate.PlayerLogWatcher(log_path),
                control_mode=control_mode, precondition_timeout=2.0, step_timeout=2.0,
                interval_seconds=0.2, log_grace=0.4)
            yield flow, control, game, gui, windows, report, log_path

    def test_healthy_flow_passes_with_ordered_keys_and_evidence(self):
        with self.simulation() as (flow, control, game, gui, windows, report, _):
            result = flow.run()
            self.assertEqual(result["feature_result"], "PASS")
            self.assertEqual((result["name"], result["control"], result["blocked_reason"]),
                             ("login-gate", None, None))
            a2, a3 = result["assertions"]
            self.assertEqual([(a["id"], a["step"], a["key"], a["status"]) for a in result["assertions"]],
                             [("A2", "A2", "enter", "PASS"), ("A3", "A3", "escape", "PASS")])
            self.assertEqual(a2["sources"], ["MainMenuController.CreateRoom/ShowLoginPanel", "GD UI §4.1"])
            self.assertEqual(a3["sources"], ["MainMenuController.OnLoginBackButtonClicked", "GD UI §4.1"])
            self.assertEqual((a2["visual"]["expected"], a2["visual"]["status"]), ("absent", "observed"))
            self.assertEqual((a3["visual"]["expected"], a3["visual"]["status"]), ("present", "observed"))
            self.assertEqual(a2["log"], {"expected_line": SHOWN, "status": "observed"})
            self.assertEqual(a3["log"], {"expected_line": CLOSED, "status": "observed"})
            self.assertEqual(a3["visual"]["location"], [70, 90])
            self.assertGreaterEqual(a2["visual"]["frames"], 3)
            self.assertLessEqual(a2["started_at_seconds"], a2["ended_at_seconds"])
            self.assertEqual(gui.keys("down"), ["enter", "escape"])
            self.assertEqual(gui.keys("up"), ["enter", "escape"])
            self.assertFalse(control.held_keys)
            self.assertEqual(len(report["evidence"]), 3)  # precondition + one per step
            for assertion in result["assertions"]:
                self.assertTrue(Path(assertion["evidence_path"]).is_file())
            self.assertEqual(gui.PAUSE, 0.1)

    def test_wrong_key_fails_a2_and_never_sends_escape(self):
        with self.simulation(control_mode="wrong-key") as (flow, control, game, gui, _, _, _):
            result = flow.run()
            self.assertEqual(result["control"], "wrong-key")
            self.assertEqual(result["feature_result"], "FAIL")
            self.assertEqual([(a["id"], a["key"], a["status"]) for a in result["assertions"]],
                             [("A2", "q", "FAIL")])
            a2 = result["assertions"][0]
            self.assertEqual((a2["visual"]["status"], a2["log"]["status"]), ("contradicted", "contradicted"))
            self.assertEqual(gui.keys("down"), ["q"])
            self.assertFalse(control.held_keys)

    def test_log_line_but_inicia_stays_is_inconclusive(self):
        with self.simulation() as (flow, _, game, gui, _, _, _):
            game.hides_menu = False
            result = flow.run()
            a2 = result["assertions"][0]
            self.assertEqual((a2["visual"]["status"], a2["log"]["status"]), ("contradicted", "observed"))
            self.assertEqual((a2["status"], result["feature_result"]), ("INCONCLUSIVE", "INCONCLUSIVE"))
            self.assertEqual(gui.keys("down"), ["enter"])

    def test_inicia_disappears_without_log_line_is_inconclusive(self):
        with self.simulation() as (flow, _, game, gui, _, _, _):
            game.logs_shown = False
            result = flow.run()
            a2 = result["assertions"][0]
            self.assertEqual((a2["visual"]["status"], a2["log"]["status"]), ("observed", "contradicted"))
            self.assertEqual((a2["status"], result["feature_result"]), ("INCONCLUSIVE", "INCONCLUSIVE"))
            self.assertEqual(len(result["assertions"]), 1)
            self.assertEqual(gui.keys("down"), ["enter"])

    def test_line_written_before_mark_is_not_accepted(self):
        with self.simulation() as (flow, _, game, gui, _, _, log_path):
            game.write(SHOWN)  # stale line from an earlier run/step
            game.logs_shown = False
            result = flow.run()
            self.assertEqual(result["assertions"][0]["log"]["status"], "contradicted")
            self.assertNotEqual(result["feature_result"], "PASS")

    def test_missing_log_never_passes(self):
        with self.simulation(log_exists=False) as (flow, _, game, gui, _, _, _):
            game.logs_shown = game.logs_closed = False
            result = flow.run()
            a2 = result["assertions"][0]
            self.assertEqual((a2["visual"]["status"], a2["log"]["status"]), ("observed", "unavailable"))
            self.assertEqual((a2["status"], result["feature_result"]), ("INCONCLUSIVE", "INCONCLUSIVE"))
            self.assertEqual(gui.keys("down"), ["enter"])

    def test_one_contradicted_other_unavailable_fails_and_both_unavailable_inconclusive(self):
        with self.simulation(log_exists=False, control_mode="wrong-key") as (flow, _, _, _, _, _, _):
            result = flow.run()
            self.assertEqual(result["assertions"][0]["log"]["status"], "unavailable")
            self.assertEqual(result["feature_result"], "FAIL")
        self.assertEqual(login_gate.classify("unavailable", "unavailable"), "INCONCLUSIVE")
        self.assertEqual(login_gate.classify("observed", "contradicted"), "INCONCLUSIVE")
        self.assertEqual(login_gate.classify("contradicted", "unavailable"), "FAIL")
        self.assertEqual(login_gate.classify("unavailable", "contradicted"), "FAIL")
        self.assertEqual(login_gate.classify("observed", "observed"), "PASS")

    def test_a3_failure_is_reported_after_a2_pass(self):
        with self.simulation() as (flow, _, game, gui, _, _, _):
            game.logs_closed = False
            result = flow.run()
            self.assertEqual([a["status"] for a in result["assertions"]], ["PASS", "INCONCLUSIVE"])
            self.assertEqual(result["feature_result"], "INCONCLUSIVE")

    def test_precondition_not_reached_blocks_without_keys(self):
        with self.simulation(start_screen="login") as (flow, control, _, gui, _, report, _):
            result = flow.run()
            self.assertEqual(result["feature_result"], "BLOCKED")
            self.assertIn("INICIAR", result["blocked_reason"])
            self.assertEqual(result["assertions"], [])
            self.assertEqual(gui.keys("down") + gui.keys("up"), [])
            self.assertFalse(control.held_keys)

    def test_guard_failure_mid_flow_blocks_and_releases_keys(self):
        with self.simulation() as (flow, control, _, gui, windows, report, _):
            def lose_focus(key):
                if key == "escape":
                    windows.focus = 999
            gui.on_down = lose_focus
            result = flow.run()
            self.assertEqual(result["feature_result"], "BLOCKED")
            self.assertIn("foreground", result["blocked_reason"])
            self.assertEqual([a["status"] for a in result["assertions"]], ["PASS"])
            self.assertEqual(gui.keys("down"), ["enter", "escape"])
            self.assertEqual(gui.keys("up"), ["enter", "escape"])
            self.assertFalse(control.held_keys)
            self.assertNotIn("held_keys_unreleased", report)

    def test_deadline_too_tight_blocks_without_keys(self):
        with self.simulation(duration=0.1) as (flow, control, _, gui, _, _, _):
            result = flow.run()
            self.assertEqual(result["feature_result"], "BLOCKED")
            self.assertEqual(gui.keys("down"), [])

    def test_invalid_control_mode_rejected(self):
        with self.simulation() as (_, control, _, _, _, _, log_path):
            with self.assertRaises(ValueError):
                login_gate.LoginGateFlow(control, readiness.Detector(REFERENCE),
                                         login_gate.PlayerLogWatcher(log_path), control_mode="other")


class FlowKeyTests(unittest.TestCase):
    def test_flow_keys_are_separate_from_the_qa002_allowlist(self):
        self.assertEqual(wc.FLOW_KEYS, ("enter", "escape", "q"))
        self.assertNotIn("enter", wc.DEMO_KEYS)
        self.assertNotIn("escape", wc.DEMO_KEYS)
        self.assertEqual((wc.VIRTUAL_KEYS["enter"], wc.VIRTUAL_KEYS["escape"]), (0x0D, 0x1B))
        with self.assertRaises(ValueError):
            wc.DemoOptions("enter")

    def test_press_rejects_non_flow_key_and_physically_held_key(self):
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GUI()
        control = wc.Demo(process, 100, {"actions": [], "evidence": []}, Path("qa-x.json"),
                          wc.DemoOptions("w"), windows=windows, gui_loader=lambda: gui, clock=clock)
        control.gui, control.hwnd = gui, 101
        with self.assertRaises(ValueError):
            control.press_flow_key("w")
        windows.pressed.add("enter")
        with self.assertRaisesRegex(RuntimeError, "physically held"):
            control.press_flow_key("enter")
        self.assertEqual(gui.events, [])


class PlayerLogWatcherTests(unittest.TestCase):
    @contextmanager
    def log(self):
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            path = Path(directory) / "Player.log"
            yield path, login_gate.PlayerLogWatcher(path)

    @staticmethod
    def append(path, data):
        with open(path, "ab") as stream:
            stream.write(data)

    def test_only_bytes_after_mark_count(self):
        with self.log() as (path, watcher):
            path.write_bytes(SHOWN.encode() + b"\n")
            watcher.mark()
            self.assertFalse(watcher.contains_since_mark(SHOWN))
            self.append(path, SHOWN.encode() + b"\n")
            self.assertTrue(watcher.contains_since_mark(SHOWN))
            watcher.mark()
            self.assertFalse(watcher.contains_since_mark(SHOWN))

    def test_partial_trailing_line_is_ignored_until_completed(self):
        with self.log() as (path, watcher):
            path.write_bytes(b"old\n")
            watcher.mark()
            self.append(path, b"[MainMenuController] Login pan")
            self.assertFalse(watcher.contains_since_mark(SHOWN))
            self.append(path, b"el shown.")
            self.assertFalse(watcher.contains_since_mark(SHOWN))  # no newline yet
            self.append(path, b"\r\n")
            self.assertTrue(watcher.contains_since_mark(SHOWN))

    def test_invalid_utf8_is_replaced_not_fatal(self):
        with self.log() as (path, watcher):
            watcher.mark()
            self.append(path, b"\xff\xfe junk \x80\n" + SHOWN.encode() + b"\n")
            self.assertTrue(watcher.contains_since_mark(SHOWN))
            self.assertFalse(watcher.contains_since_mark(CLOSED))

    def test_exact_line_not_substring(self):
        with self.log() as (path, watcher):
            watcher.mark()
            self.append(path, b"prefix " + SHOWN.encode() + b" suffix\n")
            self.assertFalse(watcher.contains_since_mark(SHOWN))

    def test_missing_or_shrunk_log_is_unavailable(self):
        with self.log() as (path, watcher):
            watcher.mark()  # never created
            with self.assertRaises(login_gate.LogUnavailable):
                watcher.contains_since_mark(SHOWN)
            path.write_bytes(b"0123456789")
            watcher.mark()
            path.write_bytes(b"01")
            with self.assertRaises(login_gate.LogUnavailable):
                watcher.contains_since_mark(SHOWN)
            watcher.mark()
            path.unlink()
            with self.assertRaises(login_gate.LogUnavailable):
                watcher.contains_since_mark(SHOWN)

    def test_log_created_after_mark_is_read_from_start(self):
        with self.log() as (path, watcher):
            watcher.mark()
            path.write_bytes(SHOWN.encode() + b"\n")
            self.assertTrue(watcher.contains_since_mark(SHOWN))

    def test_never_returns_log_text(self):
        with self.log() as (path, watcher):
            watcher.mark()
            self.append(path, b"secret token=abc\n")
            self.assertIs(watcher.contains_since_mark(SHOWN), False)


class FakeFlow:
    """Stands in for LoginGateFlow: returns a canned flow dict, optionally pressing keys first."""
    def __init__(self, control, result, keys=()):
        self.control, self.result, self.keys = control, result, keys

    def run(self):
        for key in self.keys:
            self.control.press_flow_key(key, 0.05)
        return self.result


def flow_result(feature, *statuses, blocked_reason=None, precondition="observed", control=None):
    return {"name": "login-gate", "control": control, "feature_result": feature,
            "blocked_reason": blocked_reason,
            "precondition": None if precondition is None else {"status": precondition},
            "assertions": [{"id": f"A{index + 2}", "status": status}
                           for index, status in enumerate(statuses)]}


class LoginGateRunnerTests(unittest.TestCase):
    """Flow mode through runner.run with the real Demo and fakes; no desktop, no real flow."""

    @contextmanager
    def flow_run(self, result=None, *, keys=(), focus=None, command=None, options=None,
                 flow_factory=True, duration=60):
        runner = importlib.import_module("runner")
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GUI()
        if focus is not None:
            windows.focus = focus
        options = options or wc.DemoOptions(None, flow="login-gate", window_wait_seconds=3,
                                            acquire_focus=True)
        built = []

        def make_flow(demo):
            built.append(demo)
            return FakeFlow(demo, result, keys)

        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock,
                           keyboard=GUIKeyboard(gui), flow_factory=make_flow if flow_factory else None)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                report, path = runner.run(sys.executable, duration, directory, demo=options,
                                          command=command, control_factory=factory)
            yield report, path, process, gui, windows, built

    def test_options_validate_flow_fields(self):
        options = wc.DemoOptions(None, flow="login-gate", flow_control="wrong-key")
        self.assertEqual((options.key, options.flow, options.flow_control), (None, "login-gate", "wrong-key"))
        self.assertIsNone(wc.DemoOptions("w").flow)
        for kwargs in ({"flow": "other"}, {"flow": "login-gate", "flow_control": "other"},
                       {"flow_control": "wrong-key"}):
            with self.subTest(kwargs=kwargs), self.assertRaises(ValueError):
                wc.DemoOptions(None, **kwargs)
        with self.assertRaises(ValueError):
            wc.DemoOptions("w", flow="login-gate")  # a flow takes no demo key
        with self.assertRaises(ValueError):
            wc.DemoOptions(None)  # demo mode still needs its key

    def test_requested_actions_for_flow_mode(self):
        actions = wc.requested_actions(wc.DemoOptions(None, flow="login-gate"))
        self.assertEqual([a["name"] for a in actions], ["setup", "window_wait", "flow"])

    def test_preflight_flow_report_is_initialised_and_non_flow_has_none(self):
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            seen = {}

            def factory(*args):
                seen.update(json.loads(next(Path(directory).glob("qa-*.json")).read_text(encoding="utf-8")))
                raise RuntimeError("stop before the flow")
            with patch.object(runner.subprocess, "Popen", return_value=FakeProcess()):
                runner.run(sys.executable, 1, directory, control_factory=factory,
                           demo=wc.DemoOptions(None, flow="login-gate", flow_control="wrong-key"))
        self.assertEqual(seen["flow"], {"name": "login-gate", "control": "wrong-key", "feature_result": None,
                                        "blocked_reason": None, "precondition": None, "assertions": []})
        self.assertEqual(seen["functional_validation"], "performed")
        self.assertFalse(seen["gameplay_validated"])

    def test_pass_report_shape_and_supervision_result(self):
        result = flow_result("PASS", "PASS", "PASS")
        with self.flow_run(result, keys=("enter", "escape")) as (report, path, process, gui, windows, built):
            self.assertEqual(report["schema_version"], 1)
            self.assertEqual(report["flow"], result)
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["functional_validation"], "performed")
            self.assertFalse(report["gameplay_validated"])
            self.assertIsNone(report["first_failure"])
            self.assertEqual([a["name"] for a in report["actions"]],
                             ["setup", "window_wait", "flow", "flow_key_press", "flow_key_press"])
            self.assertEqual([a["status"] for a in report["actions"]], ["executed"] * 5)
            self.assertEqual([a.get("key") for a in report["actions"][3:]], ["enter", "escape"])
            self.assertEqual(report["focus"]["policy"], "acquire")
            self.assertEqual(len(built), 1)
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)
            self.assertFalse(windows.pixel_context)

    def test_fail_report_keeps_supervision_success_and_names_the_assertion(self):
        result = flow_result("FAIL", "FAIL", control="wrong-key")
        options = wc.DemoOptions(None, flow="login-gate", flow_control="wrong-key", acquire_focus=True)
        with self.flow_run(result, keys=("q",), options=options) as (report, _, _, gui, _, _):
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["flow"]["feature_result"], "FAIL")
            self.assertEqual(report["first_failure"], {"stage": "flow:A2", "error": "FAIL"})
            self.assertEqual([e[1] for e in gui.events if e[0] == "down"], ["q"])

    def test_blocked_flow_reports_precondition_stage_and_reason(self):
        reason = "precondition not reached: INICIAR not stably present within 20s (unavailable)"
        result = flow_result("BLOCKED", blocked_reason=reason, precondition="unavailable")
        with self.flow_run(result) as (report, _, _, gui, _, _):
            self.assertEqual(report["result"], "success")
            self.assertEqual(report["first_failure"], {"stage": "flow:precondition", "error": reason})
            self.assertEqual(gui.events, [])

    def test_inconclusive_a3_names_a3(self):
        with self.flow_run(flow_result("INCONCLUSIVE", "PASS", "INCONCLUSIVE")) as (report, *_):
            self.assertEqual(report["first_failure"], {"stage": "flow:A3", "error": "INCONCLUSIVE"})

    def test_failing_flow_action_is_the_first_failure_and_blocks_the_flow(self):
        # "w" is not a flow key: press_flow_key raises inside the flow action.
        with self.flow_run(flow_result("FAIL", "FAIL"), keys=("w",)) as (report, *_):
            self.assertEqual(report["first_failure"]["stage"], "flow")
            self.assertEqual(report["flow"]["feature_result"], "BLOCKED")
            self.assertIn("flow key must be one of", report["flow"]["blocked_reason"])

    def test_focus_lock_before_the_flow_blocks_it_without_keys(self):
        with self.flow_run(flow_result("PASS", "PASS", "PASS"), focus=999, keys=("enter",)) as (
                report, path, process, gui, windows, built):
            self.assertEqual(report["result"], "failure")
            self.assertEqual(report["flow"]["feature_result"], "BLOCKED")
            self.assertIn("foreground lock", report["flow"]["blocked_reason"])
            self.assertEqual(report["first_failure"]["stage"], "window_wait")
            self.assertEqual(gui.events, [])
            self.assertEqual(built, [])
            self.assertEqual([a["name"] for a in report["actions"]], ["setup", "window_wait", "flow"])
            self.assertEqual(report["actions"][2]["status"], "skipped")
            self.assertEqual(report["functional_validation"], "performed")
            self.assertEqual(json.loads(path.read_text(encoding="utf-8")), report)

    def test_launch_failure_before_the_flow_is_blocked_with_that_error(self):
        runner = importlib.import_module("runner")
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", side_effect=OSError("launch denied")):
                report, _ = runner.run(sys.executable, 5, directory,
                                       demo=wc.DemoOptions(None, flow="login-gate"))
        self.assertEqual(report["flow"]["feature_result"], "BLOCKED")
        self.assertIn("launch denied", report["flow"]["blocked_reason"])
        self.assertEqual(report["first_failure"]["stage"], "launch")

    def test_non_flow_demo_reports_are_unchanged(self):
        runner = importlib.import_module("runner")
        process, clock, windows, gui = FakeProcess(), Clock(), Windows(), GUI()

        def factory(*args):
            return wc.Demo(*args, windows=windows, gui_loader=lambda: gui, clock=clock)
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep), \
                    patch.object(wc, "verify_saved_png"):
                report, _ = runner.run(sys.executable, 3, directory, demo=wc.DemoOptions("w"),
                                       control_factory=factory)
        self.assertNotIn("flow", report)
        self.assertEqual(report["functional_validation"], "not_performed")
        self.assertFalse(report["gameplay_validated"])
        plain = {"result": "success", "functional_validation": "not_performed"}
        self.assertEqual({k: report[k] for k in plain}, plain)

    def test_plain_observation_has_no_flow_and_no_functional_validation(self):
        runner = importlib.import_module("runner")
        process, clock = FakeProcess(), Clock()
        with tempfile.TemporaryDirectory(prefix=".test-output-", dir=QA_ROOT) as directory:
            with patch.object(runner.subprocess, "Popen", return_value=process), \
                    patch.object(runner.time, "monotonic", clock.monotonic), \
                    patch.object(runner.time, "sleep", clock.sleep):
                report, _ = runner.run(sys.executable, 1, directory)
        self.assertNotIn("flow", report)
        self.assertEqual(report["functional_validation"], "not_performed")

    def test_internal_command_child_without_injected_flow_is_blocked_by_missing_player_log(self):
        with self.flow_run(command=[sys.executable, "-c", "pass"], flow_factory=False) as (
                report, _, _, gui, _, _):
            self.assertEqual(report["player_log"]["requested"], False)
            self.assertEqual(report["flow"]["feature_result"], "BLOCKED")
            self.assertIn("Player log", report["flow"]["blocked_reason"])
            self.assertEqual(gui.events, [])

    def test_default_flow_is_built_from_the_player_log_path_and_control_mode(self):
        options = wc.DemoOptions(None, flow="login-gate", flow_control="wrong-key", acquire_focus=True)
        canned = flow_result("FAIL", "FAIL", control="wrong-key")
        with patch.object(login_gate, "LoginGateFlow") as flow_class, \
                patch.object(readiness, "Detector") as detector_class:
            flow_class.return_value.run.return_value = canned
            with self.flow_run(options=options, flow_factory=False) as (report, *_):
                pass
        control, detector, watcher = flow_class.call_args.args
        self.assertEqual(flow_class.call_args.kwargs, {"control_mode": "wrong-key"})
        self.assertIs(detector, detector_class.return_value)
        detector_class.assert_called_once_with(login_gate.DEFAULT_REFERENCE)
        self.assertEqual(str(watcher.path), report["player_log"]["path"])
        self.assertEqual(report["flow"], canned)


if __name__ == "__main__":
    unittest.main()
