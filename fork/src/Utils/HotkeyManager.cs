using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace TarkovClient.Utils
{
    /// <summary>
    /// Low-Level Keyboard Hook을 사용한 시스템 전역 핫키 관리 클래스
    /// 게임의 입력 독점 모드에서도 동작합니다.
    /// </summary>
    public class HotkeyManager : IDisposable
    {
        // Low-Level Keyboard Hook 상수
        private const int WH_KEYBOARD_LL = 13;
        private const int WM_KEYDOWN = 0x0100;
        private const int WM_SYSKEYDOWN = 0x0104;

        // Win32 API
        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr SetWindowsHookEx(
            int idHook,
            LowLevelKeyboardProc lpfn,
            IntPtr hMod,
            uint dwThreadId
        );

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool UnhookWindowsHookEx(IntPtr hhk);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr CallNextHookEx(
            IntPtr hhk,
            int nCode,
            IntPtr wParam,
            IntPtr lParam
        );

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern IntPtr GetModuleHandle(string lpModuleName);

        // Keyboard Hook 델리게이트
        private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

        // Modifier keys
        private const int VK_CONTROL = 0x11;
        private const int VK_MENU = 0x12; // Alt key
        private const int VK_SHIFT = 0x10;
        private const int VK_LWIN = 0x5B;
        private const int VK_RWIN = 0x5C;

        private readonly Window _window;
        private IntPtr _hookID = IntPtr.Zero;
        private readonly LowLevelKeyboardProc _proc = HookCallback;

        // 등록된 핫키 정보
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

        /// <summary>
        /// 핫키를 등록합니다.
        /// </summary>
        /// <param name="keyString">키 문자열 (예: "F11", "Ctrl", "Alt", "T")</param>
        /// <param name="action">핫키가 눌렸을 때 실행할 액션</param>
        /// <returns>등록 성공 여부</returns>
        public bool RegisterHotkey(string keyString, Action action)
        {
            if (string.IsNullOrEmpty(keyString) || action == null)
                return false;

            try
            {
                // 기존 핫키 제거
                UnregisterAllHotkeys();

                // 키 문자열 파싱
                var (modifiers, virtualKey) = ParseKeyString(keyString);
                if (virtualKey == 0)
                    return false;

                // 핫키 정보 저장
                _registeredKeyString = keyString;
                _registeredVirtualKey = virtualKey;
                _requiresControl = (modifiers & 0x0002) != 0;
                _requiresAlt = (modifiers & 0x0001) != 0;
                _requiresShift = (modifiers & 0x0004) != 0;
                _requiresWin = (modifiers & 0x0008) != 0;
                _registeredAction = action;

                // Low-Level Hook 설치
                return InstallHook();
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Low-Level Keyboard Hook을 설치합니다.
        /// </summary>
        private bool InstallHook()
        {
            try
            {
                using (var curProcess = Process.GetCurrentProcess())
                using (var curModule = curProcess.MainModule)
                {
                    _hookID = SetWindowsHookEx(
                        WH_KEYBOARD_LL,
                        _proc,
                        GetModuleHandle(curModule.ModuleName),
                        0
                    );
                }

                bool success = _hookID != IntPtr.Zero;
                return success;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Low-Level Keyboard Hook 콜백 함수
        /// </summary>
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            try
            {
                if (nCode >= 0 && _instance != null)
                {
                    // 키 다운 이벤트만 처리
                    if (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN)
                    {
                        // 키보드 입력 정보 구조체에서 가상 키 코드 추출
                        int vkCode = Marshal.ReadInt32(lParam);

                        if (_instance.IsRegisteredHotkey((uint)vkCode))
                        {
                            // UI 스레드에서 액션 실행
                            _instance._window?.Dispatcher.BeginInvoke(_instance._registeredAction);

                            // 키 이벤트 소비 (게임에 전달하지 않음)
                            return (IntPtr)1;
                        }
                    }
                }
            }
            catch (Exception) { }

            // 등록된 핫키가 아니면 다음 Hook에 전달
            return CallNextHookEx(_instance?._hookID ?? IntPtr.Zero, nCode, wParam, lParam);
        }

        /// <summary>
        /// 현재 키 입력이 등록된 핫키인지 확인합니다.
        /// </summary>
        private bool IsRegisteredHotkey(uint vkCode)
        {
            if (_registeredVirtualKey == 0 || vkCode != _registeredVirtualKey)
                return false;

            // Modifier 키 상태 확인
            if (_requiresControl && !IsKeyPressed(VK_CONTROL))
                return false;

            if (_requiresAlt && !IsKeyPressed(VK_MENU))
                return false;

            if (_requiresShift && !IsKeyPressed(VK_SHIFT))
                return false;

            if (_requiresWin && !IsKeyPressed(VK_LWIN) && !IsKeyPressed(VK_RWIN))
                return false;

            // 필요하지 않은 modifier가 눌려있으면 false
            if (!_requiresControl && IsKeyPressed(VK_CONTROL))
                return false;

            if (!_requiresAlt && IsKeyPressed(VK_MENU))
                return false;

            if (!_requiresShift && IsKeyPressed(VK_SHIFT))
                return false;

            if (!_requiresWin && (IsKeyPressed(VK_LWIN) || IsKeyPressed(VK_RWIN)))
                return false;

            return true;
        }

        /// <summary>
        /// 특정 키가 현재 눌려있는지 확인합니다.
        /// </summary>
        [DllImport("user32.dll")]
        private static extern short GetKeyState(int nVirtKey);

        private static bool IsKeyPressed(int vkCode)
        {
            return (GetKeyState(vkCode) & 0x8000) != 0;
        }

        /// <summary>
        /// 키 문자열을 파싱하여 modifier와 virtual key를 반환합니다.
        /// </summary>
        private (uint modifiers, uint virtualKey) ParseKeyString(string keyString)
        {
            uint modifiers = 0;
            string mainKey = keyString;

            // Modifier 키 처리
            if (keyString.Contains("Ctrl+"))
            {
                modifiers |= 0x0002; // MOD_CONTROL
                mainKey = keyString.Replace("Ctrl+", "");
            }
            if (keyString.Contains("Alt+"))
            {
                modifiers |= 0x0001; // MOD_ALT
                mainKey = keyString.Replace("Alt+", "");
            }
            if (keyString.Contains("Shift+"))
            {
                modifiers |= 0x0004; // MOD_SHIFT
                mainKey = keyString.Replace("Shift+", "");
            }
            if (keyString.Contains("Win+"))
            {
                modifiers |= 0x0008; // MOD_WIN
                mainKey = keyString.Replace("Win+", "");
            }

            // 메인 키를 Virtual Key Code로 변환
            uint virtualKey = GetVirtualKeyCode(mainKey);

            return (modifiers, virtualKey);
        }

        /// <summary>
        /// 키 이름을 Virtual Key Code로 변환합니다.
        /// </summary>
        private uint GetVirtualKeyCode(string keyName)
        {
            // F1-F12 키
            if (keyName.StartsWith("F") && keyName.Length > 1)
            {
                if (
                    int.TryParse(keyName.Substring(1), out int fKeyNum)
                    && fKeyNum >= 1
                    && fKeyNum <= 12
                )
                {
                    return (uint)(0x70 + fKeyNum - 1); // VK_F1 = 0x70
                }
            }

            // 알파벳 키
            if (keyName.Length == 1 && char.IsLetter(keyName[0]))
            {
                return (uint)keyName.ToUpper()[0];
            }

            // 숫자 키
            if (keyName.Length == 1 && char.IsDigit(keyName[0]))
            {
                return (uint)keyName[0];
            }

            // 특수 키들
            return keyName.ToUpper() switch
            {
                "SPACE" => 0x20,
                "ENTER" => 0x0D,
                "ESC" => 0x1B,
                "TAB" => 0x09,
                "BACKSPACE" => 0x08,
                "DELETE" => 0x2E,
                "HOME" => 0x24,
                "END" => 0x23,
                "PAGEUP" => 0x21,
                "PAGEDOWN" => 0x22,
                "UP" => 0x26,
                "DOWN" => 0x28,
                "LEFT" => 0x25,
                "RIGHT" => 0x27,
                _ => 0,
            };
        }

        /// <summary>
        /// 모든 핫키 등록을 해제합니다.
        /// </summary>
        public void UnregisterAllHotkeys()
        {
            try
            {
                if (_hookID != IntPtr.Zero)
                {
                    UnhookWindowsHookEx(_hookID);
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

        /// <summary>
        /// 키 문자열이 유효한지 검증합니다.
        /// </summary>
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
