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
            Logger.SimpleLog($"[WindowBoundsService] SavePipModeScreen: {_pipModeScreen.DeviceName}, Bounds={_pipModeScreen.Bounds}");
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
        /// 창 위치가 화면 경계를 벗어났는지 4개 코너 체크하고 조정
        /// </summary>
        public System.Windows.Point? ClampWindowPosition(double currentLeft, double currentTop, double width, double height, double dpiScaleX, double dpiScaleY)
        {
            if (_pipModeScreen == null)
            {
                Logger.SimpleLog("[WindowBoundsService] ClampWindowPosition: No saved screen, returning null");
                return null;
            }

            var workArea = GetWorkArea(dpiScaleX, dpiScaleY);
            var windowBounds = new WindowBounds(currentLeft, currentTop, width, height);

            Logger.SimpleLog($"[WindowBoundsService] ClampWindowPosition - Window corners: {windowBounds.CornersToString()}");
            Logger.SimpleLog($"[WindowBoundsService] ClampWindowPosition - WorkArea: {workArea}");

            bool needsAdjustment = false;
            double newLeft = currentLeft;
            double newTop = currentTop;

            // 4개 코너 중 하나라도 화면 밖으로 나가면 조정

            // 오른쪽 경계 체크 (TopRight 또는 BottomRight가 초과)
            if (windowBounds.TopRight.X > workArea.Right || windowBounds.BottomRight.X > workArea.Right)
            {
                newLeft = workArea.Right - width;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Right boundary exceeded: TR.X={windowBounds.TopRight.X} or BR.X={windowBounds.BottomRight.X} > {workArea.Right}");
            }

            // 왼쪽 경계 체크 (TopLeft 또는 BottomLeft가 미달)
            if (windowBounds.TopLeft.X < workArea.Left || windowBounds.BottomLeft.X < workArea.Left)
            {
                newLeft = workArea.Left;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Left boundary exceeded: TL.X={windowBounds.TopLeft.X} or BL.X={windowBounds.BottomLeft.X} < {workArea.Left}");
            }

            // 아래쪽 경계 체크 (BottomLeft 또는 BottomRight가 초과)
            if (windowBounds.BottomLeft.Y > workArea.Bottom || windowBounds.BottomRight.Y > workArea.Bottom)
            {
                newTop = workArea.Bottom - height;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Bottom boundary exceeded: BL.Y={windowBounds.BottomLeft.Y} or BR.Y={windowBounds.BottomRight.Y} > {workArea.Bottom}");
            }

            // 위쪽 경계 체크 (TopLeft 또는 TopRight가 미달)
            if (windowBounds.TopLeft.Y < workArea.Top || windowBounds.TopRight.Y < workArea.Top)
            {
                newTop = workArea.Top;
                needsAdjustment = true;
                Logger.SimpleLog($"[WindowBoundsService] Top boundary exceeded: TL.Y={windowBounds.TopLeft.Y} or TR.Y={windowBounds.TopRight.Y} < {workArea.Top}");
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

            var bounds = _pipModeScreen.WorkingArea;

            return new Rect(
                bounds.Left / dpiScaleX,
                bounds.Top / dpiScaleY,
                bounds.Width / dpiScaleX,
                bounds.Height / dpiScaleY
            );
        }
    }
}
