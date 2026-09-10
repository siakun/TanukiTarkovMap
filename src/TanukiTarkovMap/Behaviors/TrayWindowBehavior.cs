using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;
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

                // 0. 설정 메뉴가 열려있으면 닫기
                if (_viewModel != null)
                {
                    _viewModel.IsSettingsOpen = false;
                }

                // Show()부터 비활성 표시로 열어 게임 포커스를 잠깐 가져갔다 돌려주는 일을 막는다.
                var showActivated = window.ShowActivated;
                try
                {
                    window.ShowActivated = false;
                    window.Show();
                    window.WindowState = WindowState.Normal;
                }
                finally
                {
                    window.ShowActivated = showActivated;
                }

                // 2. 즉시 ShowWindow를 SW_SHOWNOACTIVATE로 호출하여 포커스 제거
                var handle = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                PInvoke.ShowWindow(handle, PInvoke.SW_SHOWNOACTIVATE);

                // 핀이 꺼져 있어도 표시 순간에는 일반 창 중 앞으로 올린다.
                // 항상 위 고정은 사용자가 고른 경우에만 적용한다.
                var insertAfter = window.Topmost ? PInvoke.HWND_TOPMOST : PInvoke.HWND_TOP;
                if (!PInvoke.SetWindowPos(handle, insertAfter, 0, 0, 0, 0,
                    PInvoke.SWP_NOMOVE | PInvoke.SWP_NOSIZE | PInvoke.SWP_NOACTIVATE | PInvoke.SWP_SHOWWINDOW))
                    Logger.SimpleLog("[TrayWindowBehavior] Failed to raise restored window");

                // 4. TopBar를 숨긴 상태로 시작한다.
                //    트레이에서 돌아올 때는 포커스를 가져가지 않아 창 활성화도 마우스 진입도
                //    일어나지 않으므로, TopBarAnimationBehavior가 숨겨 줄 기회가 없다.
                //    마우스를 올리면 그쪽이 다시 보여 준다
                if (_topBarTransform != null && _browserContainer != null)
                {
                    _topBarTransform.Y = -20;
                    _browserContainer.Margin = new Thickness(0, 0, 0, 0);

                    // 창 투명도가 이 상태에 걸려 있어(ActualWindowOpacity) 함께 알려야 한다.
                    // 여기서는 애니메이션 없이 값을 넣으므로 메시지도 직접 보낸다
                    WeakReferenceMessenger.Default.Send(new TopBarHiddenChangedMessage(true));
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
            // 최소화된 WPF 창도 IsVisible은 true다. 보이지 않는 창은 첫 입력에서 복원한다.
            if (AssociatedObject.IsVisible && AssociatedObject.WindowState != WindowState.Minimized)
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
