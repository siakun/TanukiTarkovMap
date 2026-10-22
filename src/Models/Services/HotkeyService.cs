using System.Windows;
using TanukiTarkovMap.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.Models.Services
{
    public class HotkeyService : IDisposable
    {
        private HotkeyManager? _hotkeyManager;
        private Window? _window;
        private Action? _hotkeyAction;

        private readonly GameWindowTracker _gameTracker;
        private volatile bool _gameFocusHotkeyEnabled = true;

        internal HotkeyService(GameWindowTracker gameTracker) => _gameTracker = gameTracker;
        private bool CanHandleHotkey()
        {
            var state = _gameTracker.State;
            return AllowsHotkey(_gameFocusHotkeyEnabled, state.CanTrack,
                state.IsRunning, state.IsGameForeground, state.IsAppForeground);
        }

        public static bool AllowsHotkey(bool enabled, bool canTrack, bool running, bool gameForeground, bool appForeground)
            => !enabled || !canTrack || (running && (gameForeground || appForeground));

        public void Initialize(Window window, Action hotkeyAction)
        {
            _window = window;
            _hotkeyAction = hotkeyAction;
            _hotkeyManager = new HotkeyManager(window) { CanHandleHotkey = CanHandleHotkey };

            Logger.SimpleLog("[HotkeyService] Initialized");
        }

        public void RegisterHotkey(bool hotkeyEnabled, string hotkeyKey)
        {
            _gameFocusHotkeyEnabled = App.GetSettings().GameFocusHotkeyEnabled;
            if (_hotkeyManager == null || _hotkeyAction == null || _window == null)
            {
                Logger.SimpleLog("[HotkeyService] Not initialized, cannot register hotkey");
                return;
            }

            try
            {
                _hotkeyManager.UnregisterAllHotkeys();

                if (hotkeyEnabled && !string.IsNullOrEmpty(hotkeyKey))
                {
                    var action = _hotkeyAction;
                    var window = _window;

                    _hotkeyManager.RegisterHotkey(hotkeyKey, () =>
                    {
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
