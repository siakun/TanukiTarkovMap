using System.Windows;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 창 위치/크기 상태 관리 서비스
    /// Normal 모드와 Compact 모드의 Rect를 분리하여 관리
    ///
    /// 사용법: ServiceLocator.WindowStateManager (DI 싱글톤)
    /// </summary>
    public class WindowStateManager
    {
        /// <summary>
        /// DI 컨테이너 전용 생성자 - 외부에서 new 사용 금지
        /// ServiceLocator.CreateInstance()를 통해서만 생성
        /// </summary>
        internal WindowStateManager() { }

        private Rect _normalModeRect;
        private Rect _compactModeRect; // 모든 맵에서 동일한 Compact Rect 사용

        /// <summary>
        /// Normal 모드 창 상태
        /// </summary>
        public Rect NormalModeRect
        {
            get => _normalModeRect;
            private set => _normalModeRect = value;
        }

        /// <summary>
        /// Compact 모드 창 상태 가져오기 (모든 맵에서 동일)
        /// </summary>
        public Rect GetCompactModeRect()
        {
            // Compact Rect가 설정되지 않았으면 기본값 반환
            if (_compactModeRect.Width <= 0 || _compactModeRect.Height <= 0)
            {
                // 기본값: 300x250 크기, 위치는 -1 (미설정)
                return new Rect(-1, -1, 300, 250);
            }

            return _compactModeRect;
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
        /// Compact 모드 창 상태 업데이트 (모든 맵에서 동일)
        /// </summary>
        public void UpdateCompactModeRect(Rect rect)
        {
            _compactModeRect = rect;
            // Logger.SimpleLog($"[WindowStateManager] Compact mode updated: {rect}");
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

            // Compact 모드 로드 (단일 Rect)
            // "default" 키의 설정이 있으면 사용, 없으면 기본값
            if (settings.MapSettings != null && settings.MapSettings.ContainsKey("default"))
            {
                var compactSetting = settings.MapSettings["default"];
                _compactModeRect = new Rect(
                    compactSetting.Left,
                    compactSetting.Top,
                    compactSetting.Width > 0 ? compactSetting.Width : 300,
                    compactSetting.Height > 0 ? compactSetting.Height : 250
                );
            }
            else
            {
                // 기본값
                _compactModeRect = new Rect(-1, -1, 300, 250);
            }

            // Logger.SimpleLog($"[WindowStateManager] Loaded from settings: Normal={_normalModeRect}, Compact={_compactModeRect}");
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

            // Compact 모드 저장 (단일 Rect, "default" 키 사용)
            if (settings.MapSettings == null)
            {
                settings.MapSettings = new Dictionary<string, MapSetting>();
            }

            if (!settings.MapSettings.ContainsKey("default"))
            {
                settings.MapSettings["default"] = new MapSetting();
            }

            var compactSetting = settings.MapSettings["default"];
            compactSetting.Left = _compactModeRect.Left;
            compactSetting.Top = _compactModeRect.Top;
            compactSetting.Width = _compactModeRect.Width;
            compactSetting.Height = _compactModeRect.Height;

            // Logger.SimpleLog($"[WindowStateManager] Saved to settings: Normal={_normalModeRect}, Compact={_compactModeRect}");
        }

        /// <summary>
        /// Normal 모드 또는 Compact 모드 Rect 업데이트 및 저장
        /// </summary>
        public void UpdateAndSave(Rect rect, bool isCompactMode)
        {
            if (isCompactMode)
            {
                UpdateCompactModeRect(rect);
                // Logger.SimpleLog($"[UpdateAndSave] Compact mode saved");
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
