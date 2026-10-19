using TanukiTarkovMap.Core.Utils;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using TanukiTarkovMap.Behaviors;

namespace TanukiTarkovMap.Models.Utils
{
    public class HotkeyManager : IDisposable
    {
        private readonly Window _window;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly PInvoke.LowLevelKeyboardProc _proc = HookCallback;

        private string _registeredKeyString;
        private uint _registeredVirtualKey;
        private bool _requiresControl;
        private bool _requiresAlt;
        private bool _requiresShift;
        private bool _requiresWin;
        private Action _registeredAction;

        private static HotkeyManager _instance;

        public HotkeyManager(Window window)
        {
            _window = window;
            _instance = this;
        }

        public bool RegisterHotkey(string keyString, Action action)
        {
            if (string.IsNullOrEmpty(keyString) || action == null)
                return false;

            try
            {
                UnregisterAllHotkeys();

                var (modifiers, virtualKey) = ParseKeyString(keyString);
                if (virtualKey == 0)
                    return false;

                _registeredKeyString = keyString;
                _registeredVirtualKey = virtualKey;
                _requiresControl = (modifiers & 0x0002) != 0;
                _requiresAlt = (modifiers & 0x0001) != 0;
                _requiresShift = (modifiers & 0x0004) != 0;
                _requiresWin = (modifiers & 0x0008) != 0;
                _registeredAction = action;

                return InstallHook();
            }
            catch (Exception)
            {
                return false;
            }
        }

        private bool InstallHook()
        {
            try
            {
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _hookID = PInvoke.SetWindowsHookEx(
                        PInvoke.WH_KEYBOARD_LL,
                        _proc,
                        PInvoke.GetModuleHandle(curModule.ModuleName),
                        0
                    );
                }

                bool success = _hookID != IntPtr.Zero;
                Logger.SimpleLog($"Hook installed: {success}, HookID: {_hookID}");
                Logger.SimpleLog($"Registered Key: {_registeredKeyString} (VK: 0x{_registeredVirtualKey:X2})");
                return success;
            }
            catch (Exception ex)
            {
                Logger.Error("InstallHook error", ex);
                return false;
            }
        }

        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _instance != null)
                {
                    if (wParam == (IntPtr)PInvoke.WM_KEYDOWN || wParam == (IntPtr)PInvoke.WM_SYSKEYDOWN)
                    {
                        int vkCode = Marshal.ReadInt32(lParam);

                        if (_instance.IsRegisteredHotkey((uint)vkCode))
                        {
                            Logger.SimpleLog("Hotkey matched! Executing action...");

                            _instance._window?.Dispatcher.BeginInvoke(_instance._registeredAction);

                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("HookCallback error", ex);
            }

            return PInvoke.CallNextHookEx(_instance?._hookID ?? IntPtr.Zero, nCode, wParam, lParam);
        }

        private bool IsRegisteredHotkey(uint vkCode)
        {
            if (HotkeyInputBehavior.IsInInputMode)
                return false;

            if (_registeredVirtualKey == 0 || vkCode != _registeredVirtualKey)
                return false;

            if (_requiresControl && !IsKeyPressed(PInvoke.VK_CONTROL))
                return false;

            if (_requiresAlt && !IsKeyPressed(PInvoke.VK_MENU))
                return false;

            if (_requiresShift && !IsKeyPressed(PInvoke.VK_SHIFT))
                return false;

            if (_requiresWin && !IsKeyPressed(PInvoke.VK_LWIN) && !IsKeyPressed(PInvoke.VK_RWIN))
                return false;

            if (!_requiresControl && IsKeyPressed(PInvoke.VK_CONTROL))
                return false;

            if (!_requiresAlt && IsKeyPressed(PInvoke.VK_MENU))
                return false;

            if (!_requiresShift && IsKeyPressed(PInvoke.VK_SHIFT))
                return false;

            if (!_requiresWin && (IsKeyPressed(PInvoke.VK_LWIN) || IsKeyPressed(PInvoke.VK_RWIN)))
                return false;

            return true;
        }

        private static bool IsKeyPressed(int vkCode)
        {
            return (PInvoke.GetKeyState(vkCode) & 0x8000) != 0;
        }

        private (uint modifiers, uint virtualKey) ParseKeyString(string keyString)
        {
            uint modifiers = 0;
            string mainKey = keyString;

            if (keyString.Contains("Ctrl+"))
            {
                modifiers |= 0x0002;
                mainKey = keyString.Replace("Ctrl+", "");
            }
            if (keyString.Contains("Alt+"))
            {
                modifiers |= 0x0001;
                mainKey = keyString.Replace("Alt+", "");
            }
            if (keyString.Contains("Shift+"))
            {
                modifiers |= 0x0004;
                mainKey = keyString.Replace("Shift+", "");
            }
            if (keyString.Contains("Win+"))
            {
                modifiers |= 0x0008;
                mainKey = keyString.Replace("Win+", "");
            }

            uint virtualKey = GetVirtualKeyCode(mainKey);

            return (modifiers, virtualKey);
        }

        private uint GetVirtualKeyCode(string keyName)
        {
            if (Enum.TryParse<Key>(keyName, ignoreCase: true, out Key key))
            {
                int vk = KeyInterop.VirtualKeyFromKey(key);
                if (vk != 0)
                    return (uint)vk;
            }

            if (keyName.Length == 1 && char.IsDigit(keyName[0]))
            {
                if (Enum.TryParse<Key>($"D{keyName}", out Key digitKey))
                    return (uint)KeyInterop.VirtualKeyFromKey(digitKey);
            }

            return 0;
        }

        public void UnregisterAllHotkeys()
        {
            try
            {
                if (_hookID != IntPtr.Zero)
                {
                    PInvoke.UnhookWindowsHookEx(_hookID);
                    _hookID = IntPtr.Zero;
                }

                _registeredKeyString = null;
                _registeredVirtualKey = 0;
                _requiresControl = false;
                _requiresAlt = false;
                _requiresShift = false;
                _requiresWin = false;
                _registeredAction = null;
            }
            catch (Exception) { }
        }

        public static bool IsValidKeyString(string keyString)
        {
            if (string.IsNullOrWhiteSpace(keyString))
                return false;

            try
            {
                var manager = new HotkeyManager(null);
                var (modifiers, virtualKey) = manager.ParseKeyString(keyString);
                return virtualKey != 0;
            }
            catch
            {
                return false;
            }
        }

        public void Dispose()
        {
            UnregisterAllHotkeys();
            _instance = null;
        }
    }
}
