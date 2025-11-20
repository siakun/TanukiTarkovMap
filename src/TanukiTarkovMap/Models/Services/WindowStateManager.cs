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
        private Dictionary<string, Rect> _pipModeRects = new(); // 맵별 PIP Rect

        /// <summary>
        /// Normal 모드 창 상태
        /// </summary>
        public Rect NormalModeRect
        {
            get => _normalModeRect;
            private set => _normalModeRect = value;
        }

        /// <summary>
        /// 현재 맵의 PIP 모드 창 상태 가져오기
        /// </summary>
        public Rect GetPipModeRect(string mapName)
        {
            // mapName이 null이면 "default" 사용
            string mapKey = string.IsNullOrEmpty(mapName) ? "default" : mapName;

            if (!_pipModeRects.ContainsKey(mapKey))
            {
                // 기본값: 300x250 크기, 위치는 -1 (미설정)
                return new Rect(-1, -1, 300, 250);
            }

            return _pipModeRects[mapKey];
        }

        /// <summary>
        /// Normal 모드 창 상태 업데이트
        /// </summary>
        public void UpdateNormalModeRect(Rect rect)
        {
            _normalModeRect = rect;
            Logger.SimpleLog($"[WindowStateManager] Normal mode updated: {rect}");
        }

        /// <summary>
        /// PIP 모드 창 상태 업데이트 (맵별)
        /// </summary>
        public void UpdatePipModeRect(string mapName, Rect rect)
        {
            // mapName이 null이면 "default" 사용
            string mapKey = string.IsNullOrEmpty(mapName) ? "default" : mapName;

            _pipModeRects[mapKey] = rect;
            Logger.SimpleLog($"[WindowStateManager] PIP mode updated for {mapKey}: {rect}");
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

            // PIP 모드 로드 (맵별)
            _pipModeRects.Clear();
            if (settings.MapSettings != null)
            {
                foreach (var kvp in settings.MapSettings)
                {
                    var mapSetting = kvp.Value;
                    _pipModeRects[kvp.Key] = new Rect(
                        mapSetting.Left,
                        mapSetting.Top,
                        mapSetting.Width > 0 ? mapSetting.Width : 300,
                        mapSetting.Height > 0 ? mapSetting.Height : 250
                    );
                }
            }

            Logger.SimpleLog($"[WindowStateManager] Loaded from settings: Normal={_normalModeRect}, PIP maps={_pipModeRects.Count}");
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

            // PIP 모드 저장 (맵별)
            if (settings.MapSettings == null)
            {
                settings.MapSettings = new Dictionary<string, MapSetting>();
            }

            foreach (var kvp in _pipModeRects)
            {
                if (!settings.MapSettings.ContainsKey(kvp.Key))
                {
                    settings.MapSettings[kvp.Key] = new MapSetting();
                }

                var mapSetting = settings.MapSettings[kvp.Key];
                mapSetting.Left = kvp.Value.Left;
                mapSetting.Top = kvp.Value.Top;
                mapSetting.Width = kvp.Value.Width;
                mapSetting.Height = kvp.Value.Height;
            }

            Logger.SimpleLog($"[WindowStateManager] Saved to settings: Normal={_normalModeRect}, PIP maps={_pipModeRects.Count}");
        }

        /// <summary>
        /// Normal 모드 또는 PIP 모드 Rect 업데이트 및 저장
        /// </summary>
        public void UpdateAndSave(Rect rect, bool isPipMode, string? currentMap = null)
        {
            if (isPipMode)
            {
                // currentMap이 null이면 "default" 사용
                string mapKey = string.IsNullOrEmpty(currentMap) ? "default" : currentMap;
                UpdatePipModeRect(mapKey, rect);
                Logger.SimpleLog($"[UpdateAndSave] PIP mode saved for map: {mapKey}");
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
