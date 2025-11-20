using System.Windows;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 창 위치/크기 상태 관리 서비스
    /// Normal 모드와 PIP 모드의 Rect를 분리하여 관리
    /// </summary>
    public class WindowStateManager
    {
        private Rect _normalModeRect;
        private Rect _pipModeRect; // 모든 맵에서 동일한 PIP Rect 사용

        /// <summary>
        /// Normal 모드 창 상태
        /// </summary>
        public Rect NormalModeRect
        {
            get => _normalModeRect;
            private set => _normalModeRect = value;
        }

        /// <summary>
        /// PIP 모드 창 상태 가져오기 (모든 맵에서 동일)
        /// </summary>
        public Rect GetPipModeRect()
        {
            // PIP Rect가 설정되지 않았으면 기본값 반환
            if (_pipModeRect.Width <= 0 || _pipModeRect.Height <= 0)
            {
                // 기본값: 300x250 크기, 위치는 -1 (미설정)
                return new Rect(-1, -1, 300, 250);
            }

            return _pipModeRect;
        }

        /// <summary>
        /// Normal 모드 창 상태 업데이트
        /// </summary>
        public void UpdateNormalModeRect(Rect rect)
        {
            _normalModeRect = rect;
            // Logger.SimpleLog($"[WindowStateManager] Normal mode updated: {rect}");
        }

        /// <summary>
        /// PIP 모드 창 상태 업데이트 (모든 맵에서 동일)
        /// </summary>
        public void UpdatePipModeRect(Rect rect)
        {
            _pipModeRect = rect;
            // Logger.SimpleLog($"[WindowStateManager] PIP mode updated: {rect}");
        }

        /// <summary>
        /// 설정에서 상태 로드
        /// </summary>
        public void LoadFromSettings(AppSettings settings)
        {
            // Normal 모드 로드
            _normalModeRect = new Rect(
                settings.NormalLeft,
                settings.NormalTop,
                settings.NormalWidth > 0 ? settings.NormalWidth : 1000,
                settings.NormalHeight > 0 ? settings.NormalHeight : 700
            );

            // PIP 모드 로드 (단일 Rect)
            // "default" 키의 설정이 있으면 사용, 없으면 기본값
            if (settings.MapSettings != null && settings.MapSettings.ContainsKey("default"))
            {
                var pipSetting = settings.MapSettings["default"];
                _pipModeRect = new Rect(
                    pipSetting.Left,
                    pipSetting.Top,
                    pipSetting.Width > 0 ? pipSetting.Width : 300,
                    pipSetting.Height > 0 ? pipSetting.Height : 250
                );
            }
            else
            {
                // 기본값
                _pipModeRect = new Rect(-1, -1, 300, 250);
            }

            // Logger.SimpleLog($"[WindowStateManager] Loaded from settings: Normal={_normalModeRect}, PIP={_pipModeRect}");
        }

        /// <summary>
        /// 설정에 상태 저장
        /// </summary>
        public void SaveToSettings(AppSettings settings)
        {
            // Normal 모드 저장
            settings.NormalLeft = _normalModeRect.Left;
            settings.NormalTop = _normalModeRect.Top;
            settings.NormalWidth = _normalModeRect.Width;
            settings.NormalHeight = _normalModeRect.Height;

            // PIP 모드 저장 (단일 Rect, "default" 키 사용)
            if (settings.MapSettings == null)
            {
                settings.MapSettings = new Dictionary<string, MapSetting>();
            }

            if (!settings.MapSettings.ContainsKey("default"))
            {
                settings.MapSettings["default"] = new MapSetting();
            }

            var pipSetting = settings.MapSettings["default"];
            pipSetting.Left = _pipModeRect.Left;
            pipSetting.Top = _pipModeRect.Top;
            pipSetting.Width = _pipModeRect.Width;
            pipSetting.Height = _pipModeRect.Height;

            // Logger.SimpleLog($"[WindowStateManager] Saved to settings: Normal={_normalModeRect}, PIP={_pipModeRect}");
        }

        /// <summary>
        /// Normal 모드 또는 PIP 모드 Rect 업데이트 및 저장
        /// </summary>
        public void UpdateAndSave(Rect rect, bool isPipMode)
        {
            if (isPipMode)
            {
                UpdatePipModeRect(rect);
                // Logger.SimpleLog($"[UpdateAndSave] PIP mode saved");
            }
            else
            {
                UpdateNormalModeRect(rect);
            }

            // 즉시 저장
            var settings = App.GetSettings();
            SaveToSettings(settings);
            App.SetSettings(settings);
            Settings.Save();
        }
    }
}
