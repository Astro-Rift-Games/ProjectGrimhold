"""E4 login-gate flow: INICIAR visual oracle plus Player log lines, composed from Demo primitives.

Expects an already set-up `Demo` (owned window found and focused, `gui` and `hwnd` set).
Importing never touches a desktop. After any non-PASS assertion no further keys are sent.
"""
import os
from pathlib import Path

from readiness import DEADLINE_MARGIN, wait_stable_state

FLOW_NAME = "login-gate"
CONTROLS = (None, "wrong-key")
DEFAULT_REFERENCE = Path(__file__).resolve().parent / "references" / "main-menu-iniciar.png"
LOG_SHOWN = "[MainMenuController] Login panel shown."
LOG_CLOSED = "[MainMenuController] Login panel closed."
MAX_APPENDED_BYTES = 16 * 1024 * 1024
LOG_POLL_SECONDS = 0.05
KEY_HOLD_SECONDS = 0.1
A2_SOURCES = ["MainMenuController.CreateRoom/ShowLoginPanel", "GD UI §4.1"]
A3_SOURCES = ["MainMenuController.OnLoginBackButtonClicked", "GD UI §4.1"]


class LogUnavailable(Exception):
    """The Player log cannot be trusted for this step (missing, unreadable, shrunk)."""


class PlayerLogWatcher:
    """Reads only bytes appended after `mark()`; never returns log text."""
    def __init__(self, path):
        self.path = Path(path)
        self._offset = None

    def mark(self):
        try:
            self._offset = self.path.stat().st_size
        except FileNotFoundError:
            self._offset = 0  # a log created later is read from its start
        except OSError:
            self._offset = None

    def contains_since_mark(self, line):
        if self._offset is None:
            raise LogUnavailable("log was not markable")
        try:
            with open(self.path, "rb") as stream:  # shared read: the game keeps writing
                size = os.fstat(stream.fileno()).st_size
                if size < self._offset:
                    raise LogUnavailable("log shrank after mark")
                if size - self._offset > MAX_APPENDED_BYTES:
                    raise LogUnavailable("appended log exceeds bounded read size")
                stream.seek(self._offset)
                data = stream.read(size - self._offset)
        except FileNotFoundError as exc:
            raise LogUnavailable("log is missing") from exc
        except OSError as exc:
            raise LogUnavailable(f"log unreadable: {type(exc).__name__}") from exc
        # The last element is an unterminated (possibly partial) line: ignore until completed.
        lines = data.decode("utf-8", errors="replace").split("\n")[:-1]
        return any(item.rstrip("\r") == line for item in lines)


def classify(visual, log):
    """Assertion status from the two independent signal statuses."""
    if visual == "observed" and log == "observed":
        return "PASS"
    if visual == "contradicted" and (log == "contradicted" or log == "unavailable"):
        return "FAIL"
    if visual == "unavailable" and log == "contradicted":
        return "FAIL"
    return "INCONCLUSIVE"


def initial_flow(control_mode=None):
    """The `flow` report dict before anything ran: feature_result stays None until resolved."""
    return {"name": FLOW_NAME, "control": control_mode, "feature_result": None,
            "blocked_reason": None, "precondition": None, "assertions": []}


class LoginGateFlow:
    def __init__(self, control, detector, watcher, *, control_mode=None, precondition_timeout=20.0,
                 step_timeout=10.0, interval_seconds=0.2, consistent_count=3,
                 key_hold_seconds=KEY_HOLD_SECONDS, log_grace=2.0):
        if control_mode not in CONTROLS:
            raise ValueError("control must be None or 'wrong-key'")
        self.control, self.detector, self.watcher = control, detector, watcher
        self.control_mode = control_mode
        self.precondition_timeout, self.step_timeout = precondition_timeout, step_timeout
        self.interval_seconds, self.consistent_count = interval_seconds, consistent_count
        self.key_hold_seconds, self.log_grace = key_hold_seconds, log_grace

    def run(self):
        """Return the `flow` report dict. Never raises except KeyboardInterrupt (after recording)."""
        control = self.control
        flow = initial_flow(self.control_mode)
        previous_pause = None
        try:
            if control.gui is None or control.hwnd is None:
                raise RuntimeError("owned window and GUI must be set up before the flow")
            previous_pause, control.gui.PAUSE = control.gui.PAUSE, 0
            self._precondition(flow)
            if flow["blocked_reason"] is None:
                a2 = self._step(flow, "A2", "q" if self.control_mode == "wrong-key" else "enter",
                                False, LOG_SHOWN, A2_SOURCES)
                if a2 == "PASS":
                    self._step(flow, "A3", "escape", True, LOG_CLOSED, A3_SOURCES)
        except (Exception, KeyboardInterrupt) as exc:
            flow["blocked_reason"] = f"{type(exc).__name__}: {exc}"
            self._release(flow)
            if isinstance(exc, KeyboardInterrupt):
                flow["feature_result"] = "BLOCKED"
                raise
        finally:
            if previous_pause is not None:
                control.gui.PAUSE = previous_pause
        statuses = [a["status"] for a in flow["assertions"]]
        if flow["blocked_reason"] is not None:
            flow["feature_result"] = "BLOCKED"
        elif statuses == ["PASS", "PASS"]:
            flow["feature_result"] = "PASS"
        else:
            flow["feature_result"] = next(status for status in statuses if status != "PASS")
        return flow

    def _release(self, flow):
        try:
            errors, _ = self.control.release_keys()
        except (Exception, KeyboardInterrupt) as exc:
            errors = [f"{type(exc).__name__}: {exc}"]
        if errors:
            flow["release_errors"] = errors

    def _wait(self, present, timeout):
        return wait_stable_state(self.control, self.detector, present=present, timeout_seconds=timeout,
                                 interval_seconds=self.interval_seconds,
                                 consistent_count=self.consistent_count)

    def _save_frame(self, visual):
        image, region = visual.pop("frame")
        self.control.save_image(image, region, {}, client=True)
        return self.control.report["evidence"][-1]["path"]

    def _precondition(self, flow):
        visual = self._wait(True, self.precondition_timeout)
        result = {"expected": "present", "status": visual["status"], "frames": visual["frames"],
                  "location": visual["location"], "evidence_path": None}
        flow["precondition"] = result
        result["evidence_path"] = self._save_frame(visual)
        if visual["status"] != "observed":
            flow["blocked_reason"] = (f"precondition not reached: INICIAR not stably present "
                                      f"within {self.precondition_timeout:g}s ({visual['status']})")

    def _log_status(self, line):
        control = self.control
        end = min(control.clock.monotonic() + self.log_grace, control.deadline - DEADLINE_MARGIN)
        while True:
            try:
                if self.watcher.contains_since_mark(line):
                    return "observed"
            except LogUnavailable:
                return "unavailable"
            now = control.clock.monotonic()
            if now >= end:
                return "contradicted"
            control.guard()
            control.clock.sleep(max(0, min(LOG_POLL_SECONDS, end - now)))

    def _step(self, flow, step, key, visual_present, line, sources):
        control = self.control
        started = control._elapsed()
        self.watcher.mark()
        control.press_flow_key(key, self.key_hold_seconds)
        visual = self._wait(visual_present, self.step_timeout)
        log = self._log_status(line)
        assertion = {
            "id": step, "step": step, "key": key,
            "expected": "INICIAR absent and log line" if not visual_present else "INICIAR present and log line",
            "sources": sources,
            "visual": {"expected": "present" if visual_present else "absent", "status": visual["status"],
                       "frames": visual["frames"], "location": visual["location"]},
            "log": {"expected_line": line, "status": log},
            "status": classify(visual["status"], log), "evidence_path": None,
            "started_at_seconds": started, "ended_at_seconds": None}
        try:
            assertion["evidence_path"] = self._save_frame(visual)
        except (Exception, KeyboardInterrupt) as exc:
            # Mandatory evidence is invalid: never leave a PASS/FAIL standing on it.
            assertion.update(status="BLOCKED", evidence_error=f"{type(exc).__name__}: {exc}",
                             ended_at_seconds=control._elapsed())
            flow["assertions"].append(assertion)
            raise
        assertion["ended_at_seconds"] = control._elapsed()
        flow["assertions"].append(assertion)
        return assertion["status"]
