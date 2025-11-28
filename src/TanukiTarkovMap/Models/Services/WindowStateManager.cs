using System.Windows;
using TanukiTarkovMap.Models.Data;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 창 위치/크기 상태 관리 서비스
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

        /// <summary>
        /// Normal 모드 창 상태
        /// </summary>
        public Rect NormalModeRect
        {
            get => _normalModeRect;
            private set => _normalModeRect = value;
        }

        /// <summary>
        /// Normal 모드 창 상태 업데이트
        /// </summary>
        public void UpdateNormalModeRect(Rect rect)
        {
            _normalModeRect = rect;
        }

        /// <summary>
        /// 설정에서 상태 로드
        /// </summary>
        public void LoadFromSettings(AppSettings settings)
        {
            _normalModeRect = new Rect(
                settings.NormalLeft,
                settings.NormalTop,
                settings.NormalWidth > 0 ? settings.NormalWidth : 1000,
                settings.NormalHeight > 0 ? settings.NormalHeight : 700
            );
        }

        /// <summary>
        /// 설정에 상태 저장
        /// </summary>
        public void SaveToSettings(AppSettings settings)
        {
            settings.NormalLeft = _normalModeRect.Left;
            settings.NormalTop = _normalModeRect.Top;
            settings.NormalWidth = _normalModeRect.Width;
            settings.NormalHeight = _normalModeRect.Height;
        }
    }
}
