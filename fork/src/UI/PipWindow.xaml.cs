using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;

namespace TarkovClient
{
    public partial class PipWindow : Window
    {
        private PipController _controller;
        private System.Windows.Threading.DispatcherTimer _fadeOutTimer;

        public WebView2 WebView => PipWebView;

        public PipWindow(PipController controller)
        {
            InitializeComponent();
            _controller = controller;

            // 페이드아웃 타이머 설정
            _fadeOutTimer = new System.Windows.Threading.DispatcherTimer();
            _fadeOutTimer.Interval = TimeSpan.FromSeconds(2);
            _fadeOutTimer.Tick += (s, e) => FadeOutControls();

            // 윈도우 이벤트
            this.LocationChanged += OnLocationChanged;
            this.SizeChanged += OnSizeChanged;
        }

        // WebView2 초기화
        public async Task InitializeWebView()
        {
            try
            {
                // 메인 창과 동일한 UserDataFolder 사용
                var userDataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "TarkovClient",
                    "WebView2"
                );

                var environment = await CoreWebView2Environment.CreateAsync(null, userDataFolder);

                await PipWebView.EnsureCoreWebView2Async(environment);

                // PiP 전용 최적화 설정
                PipWebView.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PipWebView.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PipWebView.CoreWebView2.Settings.IsSwipeNavigationEnabled = false;
                PipWebView.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                PipWebView.CoreWebView2.Settings.AreHostObjectsAllowed = false;

                // 메인 창과 동일한 URL 로드
                PipWebView.Source = new Uri(Env.WebsiteUrl);
            }
            catch (Exception) { }
        }

        #region 호버 인터랙션

        // 마우스 진입
        private void Window_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _fadeOutTimer.Stop();
            FadeInControls();
        }

        // 마우스 벗어남
        private void Window_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
        {
            _fadeOutTimer.Start();
        }

        // 제어 패널 페이드인
        private void FadeInControls()
        {
            var fadeIn = new DoubleAnimation(1.0, TimeSpan.FromMilliseconds(300));
            ControlOverlay.BeginAnimation(OpacityProperty, fadeIn);
        }

        // 제어 패널 페이드아웃
        private void FadeOutControls()
        {
            _fadeOutTimer.Stop();
            var fadeOut = new DoubleAnimation(0.0, TimeSpan.FromMilliseconds(300));
            ControlOverlay.BeginAnimation(OpacityProperty, fadeOut);
        }

        #endregion

        #region 창 조작

        // 드래그 영역 클릭 - 창 이동
        private void DragArea_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                this.DragMove();
            }
        }

        // 크기 조절 핸들 클릭
        private void ResizeHandle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left)
            {
                // Windows API를 사용한 크기 조절
                ResizeWindow();
            }
        }

        // Windows API를 통한 창 크기 조절
        private void ResizeWindow()
        {
            var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;

            // 크기 조절 시작
            var msg = System.Windows.Interop.HwndSource.FromHwnd(hwnd);
            if (msg != null)
            {
                // 마우스 커서를 우하단 모서리로 설정
                this.Cursor = System.Windows.Input.Cursors.SizeNWSE;

                // 마우스 이벤트로 크기 조절 처리
                this.MouseMove += OnResizeMouseMove;
                this.MouseLeftButtonUp += OnResizeMouseUp;
                this.CaptureMouse();
            }
        }

        private void OnResizeMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var position = e.GetPosition(this);

                // 최소/최대 크기 제한
                var newWidth = Math.Max(MinWidth, Math.Min(MaxWidth, position.X + 10));
                var newHeight = Math.Max(MinHeight, Math.Min(MaxHeight, position.Y + 10));

                this.Width = newWidth;
                this.Height = newHeight;
            }
        }

        private void OnResizeMouseUp(object sender, MouseButtonEventArgs e)
        {
            this.Cursor = System.Windows.Input.Cursors.Arrow;
            this.MouseMove -= OnResizeMouseMove;
            this.MouseLeftButtonUp -= OnResizeMouseUp;
            this.ReleaseMouseCapture();

            // 크기 변경 완료
        }

        // 종료 버튼 클릭
        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            _controller?.HidePip();
        }

        #endregion

        #region 설정 저장 이벤트

        // 위치 변경 시
        private void OnLocationChanged(object sender, EventArgs e)
        {
            // 브라우저 네이티브 PiP에서는 위치 저장 불필요
        }

        // 크기 변경 시
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            // 브라우저 네이티브 PiP에서는 크기 저장 불필요
        }

        #endregion

        // 윈도우 닫기 시 정리
        protected override void OnClosed(EventArgs e)
        {
            _fadeOutTimer?.Stop();
            PipWebView?.Dispose();
            base.OnClosed(e);
        }
    }
}
