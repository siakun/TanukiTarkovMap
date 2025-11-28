using System.Windows;
using TanukiTarkovMap.Behaviors;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 전역 핫키 관리 서비스
    /// HotkeyManager를 래핑하여 DI 싱글톤으로 관리
    ///
    /// 사용법: ServiceLocator.HotkeyService (DI 싱글톤)
    /// </summary>
    public class HotkeyService : IDisposable
    {
        private HotkeyManager? _hotkeyManager;
        private Window? _window;
        private Action? _hotkeyAction;

        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal HotkeyService() { }

        /// <summary>
        /// 핫키 서비스 초기화
        /// </summary>
        /// <param name="window">핫키를 등록할 윈도우</param>
        /// <param name="hotkeyAction">핫키가 눌렸을 때 실행할 액션</param>
        public void Initialize(Window window, Action hotkeyAction)
        {
            _window = window;
            _hotkeyAction = hotkeyAction;
            _hotkeyManager = new HotkeyManager(window);

            Logger.SimpleLog("[HotkeyService] Initialized");
        }

        /// <summary>
        /// 현재 설정에 따라 핫키 등록
        /// </summary>
        /// <param name="hotkeyEnabled">핫키 활성화 여부</param>
        /// <param name="hotkeyKey">핫키 키 문자열</param>
        public void RegisterHotkey(bool hotkeyEnabled, string hotkeyKey)
        {
            if (_hotkeyManager == null || _hotkeyAction == null || _window == null)
            {
                Logger.SimpleLog("[HotkeyService] Not initialized, cannot register hotkey");
                return;
            }

            try
            {
                // 기존 핫키 해제
                _hotkeyManager.UnregisterAllHotkeys();

                if (hotkeyEnabled && !string.IsNullOrEmpty(hotkeyKey))
                {
                    var action = _hotkeyAction;
                    var window = _window;

                    _hotkeyManager.RegisterHotkey(hotkeyKey, () =>
                    {
                        // 핫키 입력 모드일 때는 무시
                        if (HotkeyInputBehavior.IsInInputMode)
                            return;

                        Logger.SimpleLog("Global hotkey triggered");
                        window.Dispatcher.Invoke(action);
                    });

                    Logger.SimpleLog($"[HotkeyService] Hotkey registered: {hotkeyKey}");
                }
            }
            catch (Exception ex)
            {
                Logger.Error("[HotkeyService] Failed to register hotkey", ex);
            }
        }

        /// <summary>
        /// 핫키 설정 업데이트 (설정 변경 시 호출)
        /// </summary>
        /// <param name="hotkeyEnabled">핫키 활성화 여부</param>
        /// <param name="hotkeyKey">핫키 키 문자열</param>
        public void UpdateHotkey(bool hotkeyEnabled, string hotkeyKey)
        {
            RegisterHotkey(hotkeyEnabled, hotkeyKey);
            Logger.SimpleLog("[HotkeyService] Hotkey settings updated");
        }

        public void Dispose()
        {
            _hotkeyManager?.Dispose();
            _hotkeyManager = null;
            _window = null;
            _hotkeyAction = null;
            Logger.SimpleLog("[HotkeyService] Disposed");
        }
    }
}
