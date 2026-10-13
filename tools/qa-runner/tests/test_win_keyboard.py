"""Scan-code keyboard for flow keys: mocked user32 only; no real input is ever injected."""
import ctypes
import unittest
from unittest.mock import Mock

import windows_control as wc

SCAN_CODES = {0x0D: 0x1C, 0x1B: 0x01, 0x51: 0x10}  # Enter, Escape, Q


def fake_user32(scan_codes=None):
    codes = SCAN_CODES if scan_codes is None else scan_codes
    api = Mock()
    api.MapVirtualKeyW = Mock(side_effect=lambda vk, map_type: codes.get(vk, 0))
    api.keybd_event = Mock(return_value=None)
    return api


class ScanCodeKeyboardTests(unittest.TestCase):
    def keyboard(self, scan_codes=None):
        from win_keyboard import ScanCodeKeyboard
        api = fake_user32(scan_codes)
        return ScanCodeKeyboard(user32=api), api

    def test_flow_keys_send_real_scan_codes(self):
        keyboard, api = self.keyboard()
        for key, vk, scan in (("enter", 0x0D, 0x1C), ("escape", 0x1B, 0x01), ("q", 0x51, 0x10)):
            api.keybd_event.reset_mock()
            self.assertEqual(keyboard.key_down(key), scan)
            self.assertEqual(keyboard.key_up(key), scan)
            self.assertEqual(api.keybd_event.call_args_list[0].args, (vk, scan, 0, 0))
            self.assertEqual(api.keybd_event.call_args_list[1].args, (vk, scan, 0x2, 0))  # KEYEVENTF_KEYUP
        api.MapVirtualKeyW.assert_any_call(0x0D, 0)  # MAPVK_VK_TO_VSC

    def test_scan_code_is_exposed_without_sending(self):
        keyboard, api = self.keyboard()
        self.assertEqual(keyboard.scan_code("enter"), 0x1C)
        api.keybd_event.assert_not_called()

    def test_virtual_keys_match_windows_control(self):
        from win_keyboard import FLOW_VIRTUAL_KEYS
        self.assertEqual(set(FLOW_VIRTUAL_KEYS), set(wc.FLOW_KEYS))
        for key, vk in FLOW_VIRTUAL_KEYS.items():
            self.assertEqual(vk, wc.VIRTUAL_KEYS[key])

    def test_zero_scan_code_fails_closed_and_sends_nothing(self):
        keyboard, api = self.keyboard({})
        for call in (keyboard.key_down, keyboard.key_up, keyboard.scan_code):
            with self.assertRaisesRegex(RuntimeError, "scan code"):
                call("enter")
        api.keybd_event.assert_not_called()

    def test_non_flow_key_is_rejected_and_sends_nothing(self):
        keyboard, api = self.keyboard()
        with self.assertRaises(ValueError):
            keyboard.key_down("w")
        api.keybd_event.assert_not_called()

    def test_bindings_declare_argtypes_and_restype(self):
        _, api = self.keyboard()
        self.assertEqual(api.MapVirtualKeyW.argtypes, [ctypes.c_uint, ctypes.c_uint])
        self.assertEqual(api.MapVirtualKeyW.restype, ctypes.c_uint)
        self.assertEqual(len(api.keybd_event.argtypes), 4)
        self.assertIsNone(api.keybd_event.restype)

    def test_default_construction_requires_windows(self):
        from win_keyboard import ScanCodeKeyboard
        with unittest.mock.patch("win_keyboard.sys.platform", "linux"):
            with self.assertRaisesRegex(RuntimeError, "Windows"):
                ScanCodeKeyboard()


if __name__ == "__main__":
    unittest.main()
