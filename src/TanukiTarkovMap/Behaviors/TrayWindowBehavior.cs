using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 트레이 창 표시/숨김 동작을 처리하는 Behavior
    /// 게임 플레이 중 포커스를 빼앗지 않고 창을 표시/숨김
    /// </summary>
    public class TrayWindowBehavior : Behavior<Window>
    {
        private MainWindowViewModel? _viewModel;
        private TranslateTransform? _topBarTransform;
        private Border? _browserContainer;

        #region Dependency Properties

        /// <summary>
        /// TopBar의 TranslateTransform 이름 (XAML에서 x:Name으로 지정)
        /// </summary>
        public static readonly DependencyProperty TopBarTransformNameProperty =
            DependencyProperty.Register(
                nameof(TopBarTransformName),
                typeof(string),
                typeof(TrayWindowBehavior),
                new PropertyMetadata("TopBarTransform"));

        public string TopBarTransformName
        {
            get => (string)GetValue(TopBarTransformNameProperty);
            set => SetValue(TopBarTransformNameProperty, value);
        }

        /// <summary>
        /// BrowserContainer 이름 (XAML에서 x:Name으로 지정)
        /// </summary>
        public static readonly DependencyProperty BrowserContainerNameProperty =
            DependencyProperty.Register(
                nameof(BrowserContainerName),
                typeof(string),
                typeof(TrayWindowBehavior),
                new PropertyMetadata("BrowserContainer"));

        public string BrowserContainerName
        {
            get => (string)GetValue(BrowserContainerNameProperty);
            set => SetValue(BrowserContainerNameProperty, value);
        }

        #endregion

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnWindowLoaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.Loaded -= OnWindowLoaded;
            base.OnDetaching();
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = AssociatedObject.DataContext as MainWindowViewModel;
            _topBarTransform = AssociatedObject.FindName(TopBarTransformName) as TranslateTransform;
            _browserContainer = AssociatedObject.FindName(BrowserContainerName) as Border;
        }

        /// <summary>
        /// 트레이에서 창 복원 (포커스를 가져가지 않음 - 게임 플레이 끊김 방지)
        /// </summary>
        public void ShowFromTray()
        {
            try
            {
                var window = AssociatedObject;
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;

                // 1. WPF Show() 호출하여 레이아웃 활성화
                window.Show();
                window.WindowState = WindowState.Normal;

                // 2. 즉시 ShowWindow를 SW_SHOWNOACTIVATE로 호출하여 포커스 제거
                PInvoke.ShowWindow(handle, PInvoke.SW_SHOWNOACTIVATE);

                // 3. SetWindowPos로 TopMost 설정 (SWP_NOACTIVATE 플래그로 포커스 가져가지 않음)
                if (_viewModel?.IsAlwaysOnTop == true)
                {
                    PInvoke.SetWindowPos(
                        handle,
                        PInvoke.HWND_TOPMOST,
                        0, 0, 0, 0,
                        PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE
                    );
                    Logger.SimpleLog("[TrayWindowBehavior] TopMost set without stealing focus");
                }

                // 4. 핀 모드가 활성화된 경우 TopBar를 숨긴 상태로 시작
                if (_viewModel?.IsAlwaysOnTop == true && _topBarTransform != null && _browserContainer != null)
                {
                    _topBarTransform.Y = -20;
                    _browserContainer.Margin = new Thickness(0, 0, 0, 0);
                }

                Logger.SimpleLog("[TrayWindowBehavior] Window shown without stealing focus");
            }
            catch (Exception ex)
            {
                Logger.Error("[TrayWindowBehavior] Failed to show window", ex);
            }
        }

        /// <summary>
        /// 창을 트레이로 숨김
        /// </summary>
        public void HideToTray()
        {
            try
            {
                AssociatedObject.Hide();
                Logger.SimpleLog("[TrayWindowBehavior] Window hidden to tray");
            }
            catch (Exception ex)
            {
                Logger.Error("[TrayWindowBehavior] Failed to hide window", ex);
            }
        }

        /// <summary>
        /// 창 표시 상태 토글
        /// </summary>
        public void ToggleVisibility()
        {
            if (AssociatedObject.IsVisible)
            {
                HideToTray();
            }
            else
            {
                ShowFromTray();
            }
        }
    }
}
