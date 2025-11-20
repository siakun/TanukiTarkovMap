using System.Windows;
using System.Windows.Forms;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services
{
    /// <summary>
    /// 창 위치 및 4개 코너 좌표를 나타내는 구조체
    /// </summary>
    public struct WindowBounds
    {
        public double Left { get; set; }
        public double Top { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }

        public System.Windows.Point TopLeft => new System.Windows.Point(Left, Top);
        public System.Windows.Point TopRight => new System.Windows.Point(Left + Width, Top);
        public System.Windows.Point BottomLeft => new System.Windows.Point(Left, Top + Height);
        public System.Windows.Point BottomRight => new System.Windows.Point(Left + Width, Top + Height);

        public WindowBounds(double left, double top, double width, double height)
        {
            Left = left;
            Top = top;
            Width = width;
            Height = height;
        }

        public override string ToString()
        {
            return $"WindowBounds(L={Left}, T={Top}, W={Width}, H={Height})";
        }

        public string CornersToString()
        {
            return $"TL({TopLeft.X},{TopLeft.Y}), TR({TopRight.X},{TopRight.Y}), BL({BottomLeft.X},{BottomLeft.Y}), BR({BottomRight.X},{BottomRight.Y})";
        }
    }

    /// <summary>
    /// 창 경계 처리 서비스
    /// PIP 모드에서 창이 화면 밖으로 나가지 않도록 관리
    /// </summary>
    public class WindowBoundsService
    {
        private Screen _pipModeScreen = null;

        /// <summary>
        /// PIP 모드 시작 시 현재 화면 저장 (다른 모니터로 이동 방지)
        /// </summary>
        public void SavePipModeScreen(IntPtr windowHandle)
        {
            _pipModeScreen = Screen.FromHandle(windowHandle);
            // Logger.SimpleLog($"[WindowBoundsService] SavePipModeScreen: {_pipModeScreen.DeviceName}, Bounds={_pipModeScreen.Bounds}");
        }

        /// <summary>
        /// PIP 모드 종료 시 화면 정보 초기화
        /// </summary>
        public void ClearPipModeScreen()
        {
            _pipModeScreen = null;
            Logger.SimpleLog($"[WindowBoundsService] ClearPipModeScreen: Screen info cleared");
        }

        /// <summary>
        /// 창 위치가 화면 경계를 벗어났는지 체크하고 조정
        /// 창 중심점이 속한 모니터의 경계를 벗어나지 않도록 조정 (작업 표시줄 포함)
        /// </summary>
        public System.Windows.Point? ClampWindowPosition(double currentLeft, double currentTop, double width, double height, double dpiScaleX, double dpiScaleY)
        {
            // 창 중심점 계산
            var windowCenter = new System.Drawing.Point(
                (int)(currentLeft + width / 2),
                (int)(currentTop + height / 2)
            );

            // 창 중심점이 어느 모니터에 있는지 찾기
            Screen? targetScreen = null;
            foreach (var screen in Screen.AllScreens)
            {
                var screenBounds = screen.Bounds;
                if (screenBounds.Contains(windowCenter))
                {
                    targetScreen = screen;
                    Logger.SimpleLog($"[WindowBoundsService] Window center is on screen: {screen.DeviceName}");
                    break;
                }
            }

            // 변수 선언 (먼저 선언)
            double newLeft = currentLeft;
            double newTop = currentTop;

            // 창 중심점이 모든 화면 밖이면 Primary 화면 중앙으로 이동
            if (targetScreen == null)
            {
                Logger.SimpleLog("[WindowBoundsService] Window center is outside all screens, moving to primary screen center");
                var primaryWorkArea = GetWorkAreaForScreen(Screen.PrimaryScreen, dpiScaleX, dpiScaleY);

                newLeft = primaryWorkArea.Left + (primaryWorkArea.Width - width) / 2;
                newTop = primaryWorkArea.Top + (primaryWorkArea.Height - height) / 2;

                Logger.SimpleLog($"[WindowBoundsService] CLAMPING: ({currentLeft}, {currentTop}) -> ({newLeft}, {newTop})");
                return new System.Windows.Point(newLeft, newTop);
            }

            // 창 중심점이 속한 모니터 내에서 경계 체크 (작업 표시줄 포함한 전체 화면)
            var screenRect = GetWorkAreaForScreen(targetScreen, dpiScaleX, dpiScaleY);
            var windowBounds = new WindowBounds(currentLeft, currentTop, width, height);

            bool needsAdjustment = false;

            // 오른쪽 경계 체크 (TopRight 또는 BottomRight가 초과)
            if (windowBounds.TopRight.X > screenRect.Right || windowBounds.BottomRight.X > screenRect.Right)
            {
                newLeft = screenRect.Right - width;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Right boundary exceeded: TR.X={windowBounds.TopRight.X} or BR.X={windowBounds.BottomRight.X} > {screenRect.Right}");
            }

            // 왼쪽 경계 체크 (TopLeft 또는 BottomLeft가 미달)
            if (windowBounds.TopLeft.X < screenRect.Left || windowBounds.BottomLeft.X < screenRect.Left)
            {
                newLeft = screenRect.Left;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Left boundary exceeded: TL.X={windowBounds.TopLeft.X} or BL.X={windowBounds.BottomLeft.X} < {screenRect.Left}");
            }

            // 아래쪽 경계 체크 (BottomLeft 또는 BottomRight가 초과)
            if (windowBounds.BottomLeft.Y > screenRect.Bottom || windowBounds.BottomRight.Y > screenRect.Bottom)
            {
                newTop = screenRect.Bottom - height;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Bottom boundary exceeded: BL.Y={windowBounds.BottomLeft.Y} or BR.Y={windowBounds.BottomRight.Y} > {screenRect.Bottom}");
            }

            // 위쪽 경계 체크 (TopLeft 또는 TopRight가 미달)
            if (windowBounds.TopLeft.Y < screenRect.Top || windowBounds.TopRight.Y < screenRect.Top)
            {
                newTop = screenRect.Top;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Top boundary exceeded: TL.Y={windowBounds.TopLeft.Y} or TR.Y={windowBounds.TopRight.Y} < {screenRect.Top}");
            }

            if (needsAdjustment)
            {
                Logger.SimpleLog($"[WindowBoundsService] CLAMPING: ({currentLeft}, {currentTop}) -> ({newLeft}, {newTop})");
                return new System.Windows.Point(newLeft, newTop);
            }

            return null; // 조정 불필요
        }

        /// <summary>
        /// 창을 화면 내부로 이동 (PIP 모드 진입 시 초기 위치 보정)
        /// </summary>
        public System.Windows.Point EnsureWindowWithinScreen(double currentLeft, double currentTop, double width, double height, double dpiScaleX, double dpiScaleY)
        {
            if (_pipModeScreen == null)
            {
                Logger.SimpleLog("[WindowBoundsService] EnsureWindowWithinScreen: No saved screen");
                return new System.Windows.Point(currentLeft, currentTop);
            }

            var workArea = GetWorkArea(dpiScaleX, dpiScaleY);

            double newLeft = currentLeft;
            double newTop = currentTop;
            double newWidth = width;
            double newHeight = height;

            // 창이 화면보다 크면 화면 크기로 제한
            if (newWidth > workArea.Width)
                newWidth = workArea.Width;
            if (newHeight > workArea.Height)
                newHeight = workArea.Height;

            // 경계 체크 및 조정
            if (newLeft < workArea.Left)
                newLeft = workArea.Left;
            if (newLeft + newWidth > workArea.Right)
                newLeft = workArea.Right - newWidth;
            if (newTop < workArea.Top)
                newTop = workArea.Top;
            if (newTop + newHeight > workArea.Bottom)
                newTop = workArea.Bottom - newHeight;

            Logger.SimpleLog($"[WindowBoundsService] EnsureWindowWithinScreen: ({currentLeft}, {currentTop}) -> ({newLeft}, {newTop})");

            return new System.Windows.Point(newLeft, newTop);
        }

        /// <summary>
        /// 저장된 화면의 작업 영역 가져오기 (DPI 스케일 적용)
        /// </summary>
        private Rect GetWorkArea(double dpiScaleX, double dpiScaleY)
        {
            if (_pipModeScreen == null)
                throw new InvalidOperationException("PIP mode screen not saved");

            return GetWorkAreaForScreen(_pipModeScreen, dpiScaleX, dpiScaleY);
        }

        /// <summary>
        /// 특정 화면의 전체 영역 가져오기 (DPI 스케일 적용, 작업 표시줄 포함)
        /// </summary>
        private Rect GetWorkAreaForScreen(Screen screen, double dpiScaleX, double dpiScaleY)
        {
            var bounds = screen.Bounds;

            return new Rect(
                bounds.Left / dpiScaleX,
                bounds.Top / dpiScaleY,
                bounds.Width / dpiScaleX,
                bounds.Height / dpiScaleY
            );
        }
    }
}
