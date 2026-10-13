"""Scan-code keyboard for the E4 flow keys.

PyAutoGUI sends `keybd_event(vk, 0, ...)`; Unity's Input System identifies keys by
physical scan code, so the Player ignores those events. This sends the real scan
code instead. It is used only for keys the runner presses AFTER focus was already
acquired through the guarded SetForegroundWindow path; it is never a focus technique.
"""
import ctypes
import sys

FLOW_VIRTUAL_KEYS = {"enter": 0x0D, "escape": 0x1B, "q": 0x51}
MAPVK_VK_TO_VSC = 0
KEYEVENTF_KEYUP = 0x2


class ScanCodeKeyboard:
    """key_down/key_up return the scan code sent; a zero scan code is never sent."""
    def __init__(self, *, user32=None):
        if user32 is None:
            if sys.platform != "win32":
                raise RuntimeError("scan-code keyboard is supported only on Windows")
            user32 = ctypes.WinDLL("user32", use_last_error=True)
        self.api = user32
        map_virtual_key, keybd_event = user32.MapVirtualKeyW, user32.keybd_event
        map_virtual_key.argtypes = [ctypes.c_uint, ctypes.c_uint]
        map_virtual_key.restype = ctypes.c_uint
        # bVk and bScan are BYTEs; dwExtraInfo is ULONG_PTR.
        keybd_event.argtypes = [ctypes.c_ubyte, ctypes.c_ubyte, ctypes.c_uint32, ctypes.c_size_t]
        keybd_event.restype = None

    def scan_code(self, key):
        if key not in FLOW_VIRTUAL_KEYS:
            raise ValueError(f"scan-code key must be one of: {', '.join(FLOW_VIRTUAL_KEYS)}")
        scan = int(self.api.MapVirtualKeyW(FLOW_VIRTUAL_KEYS[key], MAPVK_VK_TO_VSC))
        if scan == 0:
            raise RuntimeError(f"scan code for {key} resolved to 0; refusing to send a scan-code-0 event")
        return scan

    def _send(self, key, flags):
        scan = self.scan_code(key)
        self.api.keybd_event(FLOW_VIRTUAL_KEYS[key], scan, flags, 0)
        return scan

    def key_down(self, key):
        return self._send(key, 0)

    def key_up(self, key):
        return self._send(key, KEYEVENTF_KEYUP)
