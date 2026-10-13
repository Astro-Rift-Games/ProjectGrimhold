"""QA-002 Windows single-key demo. Importing this module never queries a desktop.

PyAutoGUI is global input, not HWND-targeted input. Guards reduce but cannot
eliminate the check/injection race. Emergency release deliberately favors
unsticking runner-owned keys over foreground isolation.
"""
from contextlib import ExitStack, contextmanager
import ctypes
from ctypes import wintypes
from dataclasses import dataclass
import importlib
import math
from pathlib import Path
import sys
import time
import uuid

DEMO_KEYS = ("w", "a", "s", "d", "q", "e", "space", "up", "down", "left", "right")
MODIFIERS = ("shift", "ctrl", "alt", "winleft", "winright")
VIRTUAL_KEYS = {"w": 0x57, "a": 0x41, "s": 0x53, "d": 0x44, "q": 0x51,
                "e": 0x45, "space": 0x20, "up": 0x26, "down": 0x28,
                "left": 0x25, "right": 0x27, "shift": 0x10, "ctrl": 0x11,
                "alt": 0x12, "winleft": 0x5B, "winright": 0x5C,
                "enter": 0x0D, "escape": 0x1B}
# Flow-only keys (E4 login-gate); the QA-002 DEMO_KEYS allowlist is unchanged.
FLOW_KEYS = ("enter", "escape", "q")
CHECK_INTERVAL = 0.02
FOCUS_ATTEMPT_INTERVAL = 0.5
FOCUS_MAX_ATTEMPTS = 4
RELEASE_ATTEMPTS = 3
ACTION_NAMES = ("setup", "window_wait", "key_down", "key_hold", "key_up", "capture_delay", "capture")


@dataclass(frozen=True)
class DemoOptions:
    key: str = None  # None only for a flow, which sends its own keys
    window_wait_seconds: float = 10.0
    hold_seconds: float = 0.2
    capture_delay_seconds: float = 0.2
    readiness: object = None
    acquire_focus: bool = False
    flow: str = None
    flow_control: str = None

    def __post_init__(self):
        from login_gate import CONTROLS, FLOW_NAME
        if self.flow not in (None, FLOW_NAME):
            raise ValueError(f"flow must be {FLOW_NAME}")
        if self.flow_control not in CONTROLS or (self.flow_control is not None and self.flow is None):
            raise ValueError("flow control must be wrong-key and requires a flow")
        if not isinstance(self.acquire_focus, bool):
            raise ValueError("acquire_focus must be a boolean")
        if self.readiness is not None:
            from readiness import ReadyOptions
            if not isinstance(self.readiness, ReadyOptions):
                raise ValueError("readiness must be a validated ReadyOptions object")
        if self.flow is not None:
            if self.key is not None:
                raise ValueError("a flow takes no demo key")
        elif self.key not in DEMO_KEYS:
            raise ValueError(f"demo key must be one of: {', '.join(DEMO_KEYS)}")
        for name, limit in (("window_wait_seconds", 60), ("hold_seconds", 10),
                            ("capture_delay_seconds", 30)):
            try:
                value = float(getattr(self, name))
            except (ValueError, TypeError, OverflowError) as exc:
                raise ValueError(f"{name} must be positive and finite, at most {limit} seconds") from exc
            if not math.isfinite(value) or not 0 < value <= limit:
                raise ValueError(f"{name} must be positive and finite, at most {limit} seconds")
            object.__setattr__(self, name, value)

    def validate_duration(self, duration):
        if self.hold_seconds + self.capture_delay_seconds >= duration:
            raise ValueError("duration must exceed key hold plus capture delay; no deadline extension is allowed")


def requested_actions(options):
    names = list(ACTION_NAMES)
    if options.flow is not None:
        names = ["setup", "window_wait", "flow"]
    elif options.readiness is not None:
        names.insert(2, "readiness")
    return [{"name": name, "requested": True, "attempted": False,
             "executed": False, "status": "skipped", "key": options.key,
             "global_input_attempted": False, "global_input_completed": False,
             "started_at_seconds": None, "ended_at_seconds": None}
            for name in names]


def load_gui():
    # Import itself can query the desktop: reachable only from explicit demo.
    try:
        return importlib.import_module("pyautogui")
    except Exception as exc:
        raise RuntimeError(f"PyAutoGUI setup failed; use the qa-runner local virtualenv: {exc}") from exc


def verify_saved_png(path, region):
    """Validate actual saved PNG format, integrity, dimensions and decoded data."""
    from PIL import Image
    with Image.open(path) as image:
        if image.format != "PNG" or image.size != tuple(region[2:]):
            raise ValueError("saved evidence must be PNG with the requested region dimensions")
        image.verify()
    with Image.open(path) as image:
        image.load()


class Win32:
    """Small ctypes boundary; constructed only by an explicitly requested demo."""
    def __init__(self, *, user32=None):
        if user32 is None:
            if sys.platform != "win32":
                raise RuntimeError("QA-002 demo is supported only on Windows")
            user32 = ctypes.WinDLL("user32", use_last_error=True)
        self.api = user32
        # HWND, LPARAM and DPI_AWARENESS_CONTEXT are pointer-sized on Win64.
        self.enum_callback = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
        signatures = {
            "EnumWindows": ([self.enum_callback, wintypes.LPARAM], wintypes.BOOL),
            "GetWindowThreadProcessId": ([wintypes.HWND, ctypes.POINTER(wintypes.DWORD)], wintypes.DWORD),
            "IsWindow": ([wintypes.HWND], wintypes.BOOL),
            "IsWindowVisible": ([wintypes.HWND], wintypes.BOOL),
            "IsIconic": ([wintypes.HWND], wintypes.BOOL),
            "GetAncestor": ([wintypes.HWND, wintypes.UINT], wintypes.HWND),
            "GetWindow": ([wintypes.HWND, wintypes.UINT], wintypes.HWND),
            "GetForegroundWindow": ([], wintypes.HWND),
            "GetWindowRect": ([wintypes.HWND, ctypes.POINTER(wintypes.RECT)], wintypes.BOOL),
            "GetSystemMetrics": ([ctypes.c_int], ctypes.c_int),
            "GetAsyncKeyState": ([ctypes.c_int], ctypes.c_short),
            "SetThreadDpiAwarenessContext": ([ctypes.c_void_p], ctypes.c_void_p),
        }
        for name, (args, result) in signatures.items():
            function = getattr(self.api, name)  # Unsupported API fails setup, not a wrong crop.
            function.argtypes, function.restype = args, result

    def _pid(self, hwnd):
        pid = wintypes.DWORD()
        if not self.api.GetWindowThreadProcessId(hwnd, ctypes.byref(pid)):
            raise RuntimeError("GetWindowThreadProcessId failed")
        return pid.value

    def _visible_root(self, hwnd):
        return (self.api.IsWindow(hwnd) and self.api.IsWindowVisible(hwnd)
                and not self.api.IsIconic(hwnd)
                and self.api.GetAncestor(hwnd, 2) == hwnd  # GA_ROOT
                and not self.api.GetWindow(hwnd, 4))  # GW_OWNER: reject owned dialogs.

    def owned_windows(self, pid):
        handles, errors = [], []
        def visit(hwnd, _):
            try:
                if self._visible_root(hwnd) and self._pid(hwnd) == pid:
                    handles.append(hwnd)
                return True
            except (Exception, KeyboardInterrupt) as exc:
                errors.append(exc)
                return False
        callback = self.enum_callback(visit)  # Keep callback alive throughout EnumWindows.
        result = self.api.EnumWindows(callback, 0)
        if errors:
            raise errors[0]
        if not result:
            raise RuntimeError("EnumWindows failed")
        return handles

    def validate(self, hwnd, pid):
        if not self._visible_root(hwnd) or self._pid(hwnd) != pid:
            raise RuntimeError("invalid or no longer owned HWND/PID")
        if self.owned_windows(pid) != [hwnd]:
            raise RuntimeError("absent or ambiguous owned root windows")

    def foreground(self):
        return self.api.GetForegroundWindow()

    def set_foreground(self, hwnd):
        # Opt-in focus acquisition only: lazily bound so the default setup is unchanged.
        function = self.api.SetForegroundWindow
        function.argtypes, function.restype = [wintypes.HWND], wintypes.BOOL
        return bool(function(hwnd))

    def physically_held(self, key):
        return bool(self.api.GetAsyncKeyState(VIRTUAL_KEYS[key]) & 0x8000)

    @contextmanager
    def physical_pixels(self):
        # Per-monitor-aware V2 avoids virtualized GetWindowRect vs Pillow pixels.
        previous = self.api.SetThreadDpiAwarenessContext(ctypes.c_void_p(-4))
        if not previous:
            raise RuntimeError("cannot establish physical-pixel DPI context")
        try:
            yield
        finally:
            if not self.api.SetThreadDpiAwarenessContext(previous):
                raise RuntimeError("cannot restore thread DPI context")

    def window_region(self, hwnd):
        rect = wintypes.RECT()
        if not self.api.GetWindowRect(hwnd, ctypes.byref(rect)):
            raise RuntimeError("GetWindowRect failed")
        x, y = rect.left, rect.top
        width, height = rect.right - x, rect.bottom - y
        screen_width, screen_height = self.api.GetSystemMetrics(0), self.api.GetSystemMetrics(1)
        # Pillow/PyScreeze's default Windows capture is primary-monitor only.
        if (x < 0 or y < 0 or width <= 0 or height <= 0
                or x + width > screen_width or y + height > screen_height):
            raise RuntimeError("owned window rectangle is not wholly within the primary monitor")
        return x, y, width, height

    def client_region(self, hwnd):
        # QA003 only: lazily bind so QA002's setup/API requirements stay unchanged.
        for name, args in (("GetClientRect", [wintypes.HWND, ctypes.POINTER(wintypes.RECT)]),
                           ("ClientToScreen", [wintypes.HWND, ctypes.POINTER(wintypes.POINT)])):
            function = getattr(self.api, name)
            function.argtypes, function.restype = args, wintypes.BOOL
        rect, origin = wintypes.RECT(), wintypes.POINT()
        if not self.api.GetClientRect(hwnd, ctypes.byref(rect)) or not self.api.ClientToScreen(hwnd, ctypes.byref(origin)):
            raise RuntimeError("cannot measure physical-pixel owned client area")
        region = (origin.x, origin.y, rect.right - rect.left, rect.bottom - rect.top)
        x, y, width, height = region
        wx, wy, ww, wh = self.window_region(hwnd)
        if width <= 0 or height <= 0 or x < wx or y < wy or x + width > wx + ww or y + height > wy + wh:
            raise RuntimeError("client area outside owned primary-monitor window")
        return region


class Demo:
    def __init__(self, process, deadline, report, report_path, options, *,
                 windows=None, gui_loader=load_gui, clock=time, origin=None, flow_factory=None, keyboard=None):
        # `origin` is the launch-start instant on `clock`; action times are relative to it.
        self.origin = origin
        self.process, self.deadline, self.report = process, deadline, report
        self.report_path, self.options = Path(report_path), options
        self.windows, self.gui_loader, self.clock = windows, gui_loader, clock
        self.gui, self.hwnd = None, None
        self.flow_factory = flow_factory or (lambda demo: demo.default_flow())
        self.keyboard = keyboard  # scan-code keyboard for flow keys; created lazily on Windows
        self.held_keys = set()
        self.flow_held = set()  # held keys that were injected by scan code and must be released that way
        from readiness import initial_report
        report.setdefault("readiness", initial_report(options.readiness))
        self.focus_report()
        if not report["actions"]:
            report["actions"] = requested_actions(options)

    def _alive(self, *, check_deadline=True):
        if self.process.poll() is not None:
            raise RuntimeError("owned process exited during demo")
        if check_deadline and self.clock.monotonic() >= self.deadline:
            raise RuntimeError("observation deadline reached during demo")

    def guard(self, *, check_deadline=True, check_modifiers=True):
        self._alive(check_deadline=check_deadline)
        self.windows.validate(self.hwnd, self.process.pid)
        if check_modifiers:
            for key in MODIFIERS:
                if self.windows.physically_held(key):
                    raise RuntimeError(f"unsafe physically held modifier: {key}")
        self._alive(check_deadline=check_deadline)
        if self.windows.foreground() != self.hwnd:
            raise RuntimeError("owned window does not have exact foreground focus")

    def _elapsed(self):
        """Launch-relative seconds on the injected clock; None when unknown."""
        if self.origin is None:
            return None
        try:
            return max(0.0, self.clock.monotonic() - self.origin)
        except Exception:
            return None

    @contextmanager
    def action(self, name, *, entry=None):
        action = entry if entry is not None else next(a for a in self.report["actions"] if a["name"] == name)
        action.update(attempted=True, executed=False, status="attempted",
                      started_at_seconds=self._elapsed())
        try:
            yield action
        except (Exception, KeyboardInterrupt) as exc:
            action.update(status="error", error=f"{type(exc).__name__}: {exc}",
                          ended_at_seconds=self._elapsed())
            raise
        else:
            action.update(executed=True, status="executed", ended_at_seconds=self._elapsed())

    def focus_report(self):
        # Looked up on the current report: callers may rebind `report` after construction.
        return self.report.setdefault("focus", {
            "policy": "acquire" if self.options.acquire_focus else "manual", "auto_foreground": False,
            "set_foreground_attempts": 0, "set_foreground_results": [],
            "acquired": False, "error": None})

    def _acquire_foreground(self, next_attempt):
        """Ask for focus on the validated owned HWND only; returns the next attempt time."""
        focus = self.focus_report()
        now = self.clock.monotonic()
        if focus["set_foreground_attempts"] >= FOCUS_MAX_ATTEMPTS or now < next_attempt:
            return next_attempt
        self.windows.validate(self.hwnd, self.process.pid)
        focus["set_foreground_results"].append(bool(self.windows.set_foreground(self.hwnd)))
        focus["set_foreground_attempts"] += 1
        return now + FOCUS_ATTEMPT_INTERVAL

    def wait_window(self):
        end = min(self.clock.monotonic() + self.options.window_wait_seconds,
                  self.deadline - self.options.hold_seconds - self.options.capture_delay_seconds)
        focus, next_attempt = self.focus_report(), 0.0
        while True:
            self._alive()
            if self.clock.monotonic() >= end:
                if self.options.acquire_focus and self.hwnd is not None:
                    focus["error"] = ("foreground lock: the owned window never received foreground focus; "
                                      "launch the runner from the foreground interactive context")
                    raise RuntimeError(focus["error"])
                raise RuntimeError("owned window/manual focus wait timeout within observation deadline")
            handles = self.windows.owned_windows(self.process.pid)
            if len(handles) > 1:
                raise RuntimeError("ambiguous visible root windows for owned PID")
            if handles:
                self.hwnd = handles[0]
                self.windows.validate(self.hwnd, self.process.pid)
                self.report["window"] = {"hwnd": int(self.hwnd), "pid": self.process.pid,
                                         "coordinate_space": "physical_pixels", "monitor": "primary_only"}
                if self.windows.foreground() == self.hwnd:
                    if self.options.acquire_focus:
                        focus["auto_foreground"] = focus["set_foreground_attempts"] == 0
                        focus["acquired"] = True
                    self.guard()
                    return
                if self.options.acquire_focus:
                    next_attempt = self._acquire_foreground(next_attempt)
            self.clock.sleep(max(0, min(CHECK_INTERVAL, end - self.clock.monotonic())))

    def guarded_wait(self, seconds):
        end = self.clock.monotonic() + seconds
        if end >= self.deadline:
            raise RuntimeError("insufficient time before observation deadline")
        while True:
            self.guard()
            self.gui.failSafeCheck()
            remaining = end - self.clock.monotonic()
            if remaining <= 0:
                return
            self.clock.sleep(max(0, min(CHECK_INTERVAL, remaining, self.deadline - self.clock.monotonic())))

    def execute(self):
        try:
            self._execute()
        except (Exception, KeyboardInterrupt) as exc:
            readiness = self.report["readiness"]
            if self.options.readiness is not None and readiness["state"] != "ready":
                readiness.update(state="blocked", error=f"{type(exc).__name__}: {exc}")
            raise

    def _execute(self):
        with ExitStack() as stack:
            with self.action("setup"):
                self._alive()
                if self.windows is None:
                    self.windows = Win32()
                stack.enter_context(self.windows.physical_pixels())
                try:
                    self.gui = self.gui_loader()
                except Exception as exc:
                    raise RuntimeError(f"PyAutoGUI dependency/setup failed: {exc}") from exc
            previous_pause = self.gui.PAUSE
            try:
                # Avoid implicit per-event delays extending the observation budget.
                self.gui.PAUSE = 0
                with self.action("window_wait"):
                    self.wait_window()
                if self.options.flow is not None:
                    with self.action("flow"):
                        self.report["flow"] = self.flow_factory(self).run()
                    return
                if self.options.readiness is not None:
                    from readiness import wait_ready
                    with self.action("readiness"):
                        wait_ready(self)
                with self.action("key_down") as action:
                    if self.windows.physically_held(self.options.key):
                        raise RuntimeError(f"demo key already physically held: {self.options.key}")
                    self.guard()
                    # Injection might happen before keyDown raises. Mark BEFORE call.
                    self.held_keys.add(self.options.key)
                    action["global_input_attempted"] = True
                    self.gui.keyDown(self.options.key)
                    action["global_input_completed"] = True
                with self.action("key_hold"):
                    self.guarded_wait(self.options.hold_seconds)
                with self.action("key_up") as action:
                    self.guard()
                    self.gui.failSafeCheck()
                    self.guard()
                    action["global_input_attempted"] = True
                    self.gui.keyUp(self.options.key)
                    self.held_keys.remove(self.options.key)
                    action["global_input_completed"] = True
                with self.action("capture_delay"):
                    self.guarded_wait(self.options.capture_delay_seconds)
                with self.action("capture") as action:
                    self.capture(action)
            finally:
                self.gui.PAUSE = previous_pause

    def default_flow(self):
        """The real login-gate flow; it needs the Player log of a real executable launch."""
        from login_gate import DEFAULT_REFERENCE, LoginGateFlow, PlayerLogWatcher
        from readiness import Detector
        player_log = self.report["player_log"]
        if not player_log.get("requested") or not player_log.get("path"):
            raise RuntimeError("flow requires the Player log of a real executable launch")
        return LoginGateFlow(self, Detector(DEFAULT_REFERENCE), PlayerLogWatcher(player_log["path"]),
                             control_mode=self.options.flow_control)

    def flow_keyboard(self):
        if self.keyboard is None:
            from win_keyboard import ScanCodeKeyboard
            self.keyboard = ScanCodeKeyboard()
        return self.keyboard

    def press_flow_key(self, key, hold_seconds=0.1):
        """One guarded down/hold/up of a flow key; a failed up stays in held_keys for release_keys."""
        if key not in FLOW_KEYS:
            raise ValueError(f"flow key must be one of: {', '.join(FLOW_KEYS)}")
        entry = {"name": "flow_key_press", "requested": True, "attempted": False,
                 "executed": False, "status": "skipped", "key": key,
                 "scan_code": None, "global_input_attempted": False,
                 "global_input_completed": False, "started_at_seconds": None,
                 "ended_at_seconds": None}
        self.report["actions"].append(entry)
        with self.action("flow_key_press", entry=entry) as action:
            if self.windows.physically_held(key):
                raise RuntimeError(f"flow key already physically held: {key}")
            self.guard()
            keyboard = self.flow_keyboard()
            action["scan_code"] = keyboard.scan_code(key)  # fails closed before anything is sent
            # Injection might happen before key_down raises. Mark BEFORE call.
            self.held_keys.add(key)
            self.flow_held.add(key)
            action["global_input_attempted"] = True
            keyboard.key_down(key)
            self.guarded_wait(hold_seconds)
            self.guard()
            self.gui.failSafeCheck()
            self.guard()
            keyboard.key_up(key)
            self.held_keys.remove(key)
            self.flow_held.remove(key)
            action["global_input_completed"] = True

    def capture(self, action):
        image, region = self.acquire_image(action)
        self.save_image(image, region, action)

    def acquire_image(self, action, *, client=False):
        region_query = self.windows.client_region if client else self.windows.window_region
        self.guard()
        region = region_query(self.hwnd)
        if client:
            from readiness import MAX_FRAME_SIZE
            if not 0 < region[2] <= MAX_FRAME_SIZE[0] or not 0 < region[3] <= MAX_FRAME_SIZE[1]:
                raise RuntimeError("client screenshot exceeds bounded 1920x1080 dimensions")
        self.guard()
        if region_query(self.hwnd) != region:
            raise RuntimeError("window geometry changed before screenshot")
        self.gui.failSafeCheck()
        self.guard()
        action["capture_attempted"] = True
        image = self.gui.screenshot(region=region)
        # Do not save the image at all if ownership/focus/geometry changed in capture.
        self.guard()
        if region_query(self.hwnd) != region:
            raise RuntimeError("window geometry changed during screenshot; evidence discarded")
        self.guard()
        if client and image.size != tuple(region[2:]):
            raise ValueError("readiness screenshot dimensions do not match owned client region")
        return image, region

    def save_image(self, image, region, action, *, purpose=None, captured_at=None, historical=False,
                   client=False):
        region_query = self.windows.client_region if client else self.windows.window_region
        if historical:
            if self.clock.monotonic() >= self.deadline:
                raise RuntimeError("global deadline reached before diagnostic save")
        while True:
            path = self.report_path.parent / f"{self.report_path.stem}-evidence-{uuid.uuid4().hex}.png"
            try:
                stream = path.open("xb")
                break
            except FileExistsError:
                continue
        # If saving/verification fails the file is not accepted evidence. Retain
        # its path as discarded (may be partial); never claim capture success.
        action["discarded_evidence_path"] = str(path)
        with stream:
            image.save(stream, format="PNG")
            stream.flush()
        verify_saved_png(path, region)
        if historical:
            if self.clock.monotonic() >= self.deadline:
                raise RuntimeError("global deadline reached while saving diagnostic; evidence discarded")
        else:
            self.guard()
            if region_query(self.hwnd) != region:
                raise RuntimeError("window geometry changed while saving; evidence discarded")
        self.report["evidence"].append({
            "path": str(path), "format": "PNG", "status": "saved", "hwnd": int(self.hwnd),
            "pid": self.process.pid, "region": list(region), "coordinate_space": "physical_pixels",
            "content_validation": "not_performed",
            "risk": "Visible desktop region may contain overlays/occlusion; not exclusive window content.",
        })
        if historical:
            self.report["evidence"][-1].update(
                purpose=purpose, role="previous_guarded_frame_not_current_authority",
                captured_at_monotonic_seconds=captured_at,
                saved_at_monotonic_seconds=self.clock.monotonic(), stale=True)
        action.pop("discarded_evidence_path")

    def release_keys(self):
        """Bounded best effort; never inject a new down or forget a failed up."""
        errors, interrupted = [], False
        for key in sorted(self.held_keys):
            action = {"name": "key_release_cleanup", "key": key, "requested": True,
                      "attempted": False, "executed": False, "status": "attempted",
                      "global_input_attempted": False, "global_input_completed": False,
                      "unsafe_global_release": False, "failsafe_bypassed_for_release": False,
                      "started_at_seconds": self._elapsed(), "ended_at_seconds": None,
                      "risk": "Emergency global release may reach another app or release a human-held key."}
            flow_key = key in self.flow_held
            if flow_key:
                action["scan_code"] = None
            self.report["actions"].append(action)
            bypass = False
            for attempt in range(RELEASE_ATTEMPTS):
                try:
                    try:
                        self.guard(check_deadline=False)
                    except (Exception, KeyboardInterrupt) as exc:
                        interrupted |= isinstance(exc, KeyboardInterrupt)
                        action["unsafe_global_release"] = True
                        action["safety_error"] = f"{type(exc).__name__}: {exc}"
                    action["attempted"] = True
                    action["global_input_attempted"] = True
                    previous_failsafe = self.gui.FAILSAFE
                    try:
                        if bypass:
                            # Only this previously attempted held key's UP may bypass.
                            self.gui.FAILSAFE = False
                            action["failsafe_bypassed_for_release"] = True
                        if flow_key:
                            # Pressed by scan code, so released by scan code (PyAutoGUI's UP has scan 0).
                            action["scan_code"] = self.keyboard.scan_code(key)
                            self.keyboard.key_up(key)
                        else:
                            self.gui.keyUp(key)
                    finally:
                        self.gui.FAILSAFE = previous_failsafe
                    self.held_keys.remove(key)
                    self.flow_held.discard(key)
                    action.update(executed=True, status="executed", global_input_completed=True)
                    break
                except (Exception, KeyboardInterrupt) as exc:
                    interrupted |= isinstance(exc, KeyboardInterrupt)
                    error = f"Key cleanup {key} attempt {attempt + 1}: {type(exc).__name__}: {exc}"
                    errors.append(error)
                    action.setdefault("errors", []).append(error)
                    bypass |= isinstance(exc, self.gui.FailSafeException)
                    action.update(status="error", error=error)
            action["ended_at_seconds"] = self._elapsed()
        if self.held_keys:
            self.report["held_keys_unreleased"] = sorted(self.held_keys)
            errors.append("Runner-owned key release failed; keys may remain held globally.")
        return errors, interrupted
