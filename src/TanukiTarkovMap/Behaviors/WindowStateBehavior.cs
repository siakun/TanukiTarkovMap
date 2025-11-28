using System.Windows;
using System.Windows.Controls;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 창 상태(최대화/복원), 위치, 크기 변경을 처리하는 Behavior
    /// MainWindow.xaml.cs의 StateChanged, LocationChanged, SizeChanged 이벤트 핸들러를 대체
    /// </summary>
    public class WindowStateBehavior : Behavior<Window>
    {
        private MainWindowViewModel? _viewModel;
        private Grid? _mainGrid;
        private Button? _maximizeRestoreButton;
        private bool _isClampingLocation = false;
        private bool _isInitializing = true;

        #region Dependency Properties

        /// <summary>
        /// MainGrid 이름 (최대화 시 여백 조정용)
        /// </summary>
        public static readonly DependencyProperty MainGridNameProperty =
            DependencyProperty.Register(
                nameof(MainGridName),
                typeof(string),
                typeof(WindowStateBehavior),
                new PropertyMetadata("MainGrid"));

        public string MainGridName
        {
            get => (string)GetValue(MainGridNameProperty);
            set => SetValue(MainGridNameProperty, value);
        }

        /// <summary>
        /// MaximizeRestoreButton 이름 (최대화/복원 아이콘 변경용)
        /// </summary>
        public static readonly DependencyProperty MaximizeRestoreButtonNameProperty =
            DependencyProperty.Register(
                nameof(MaximizeRestoreButtonName),
                typeof(string),
                typeof(WindowStateBehavior),
                new PropertyMetadata("MaximizeRestoreButton"));

        public string MaximizeRestoreButtonName
        {
            get => (string)GetValue(MaximizeRestoreButtonNameProperty);
            set => SetValue(MaximizeRestoreButtonNameProperty, value);
        }

        #endregion

        protected override void OnAttached()
        {
            base.OnAttached();

            AssociatedObject.Loaded += OnWindowLoaded;
            AssociatedObject.StateChanged += OnStateChanged;
            AssociatedObject.LocationChanged += OnLocationChanged;
            AssociatedObject.SizeChanged += OnSizeChanged;
        }

        protected override void OnDetaching()
        {
            base.OnDetaching();

            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.StateChanged -= OnStateChanged;
            AssociatedObject.LocationChanged -= OnLocationChanged;
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = AssociatedObject.DataContext as MainWindowViewModel;

            // XAML 요소 찾기
            _mainGrid = AssociatedObject.FindName(MainGridName) as Grid;
            _maximizeRestoreButton = AssociatedObject.FindName(MaximizeRestoreButtonName) as Button;

            if (_mainGrid == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MainGrid '{MainGridName}' not found");
            }

            if (_maximizeRestoreButton == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MaximizeRestoreButton '{MaximizeRestoreButtonName}' not found");
            }

            // 초기화 완료 - 약간의 지연 후 플래그 해제
            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                _isInitializing = false;
                Logger.SimpleLog("[WindowStateBehavior] Initialization complete");
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        /// <summary>
        /// WindowState 변경 시 처리 (최대화/복원 시 여백 조정 및 버튼 아이콘 변경)
        /// </summary>
        private void OnStateChanged(object? sender, EventArgs e)
        {
            var window = AssociatedObject;

            if (window.WindowState == WindowState.Maximized)
            {
                // 최대화 시 화면 경계를 넘지 않도록 여백 추가
                if (_mainGrid != null)
                {
                    var thickness = SystemParameters.WindowResizeBorderThickness;
                    _mainGrid.Margin = new Thickness(
                        thickness.Left,
                        thickness.Top,
                        thickness.Right,
                        thickness.Bottom);
                }

                // 최대화 버튼 아이콘 변경
                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "❐";
                }
            }
            else
            {
                // 일반 상태로 복원 시 여백 제거
                if (_mainGrid != null)
                {
                    _mainGrid.Margin = new Thickness(0);
                }

                // 복원 버튼 아이콘 변경
                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "□";
                }
            }
        }

        /// <summary>
        /// 창 위치 변경 시 ViewModel에 알림
        /// </summary>
        private void OnLocationChanged(object? sender, EventArgs e)
        {
            if (_isClampingLocation) return;
            if (_isInitializing) return;

            try
            {
                _isClampingLocation = true;
                NotifyWindowBoundsChanged();
            }
            finally
            {
                _isClampingLocation = false;
            }
        }

        /// <summary>
        /// 창 크기 변경 시 ViewModel에 알림
        /// </summary>
        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isClampingLocation) return;
            if (_isInitializing) return;

            NotifyWindowBoundsChanged();
        }

        /// <summary>
        /// ViewModel에 창 위치/크기 변경 알림
        /// </summary>
        private void NotifyWindowBoundsChanged()
        {
            if (_viewModel == null) return;

            var window = AssociatedObject;
            var bounds = new Rect(window.Left, window.Top, window.ActualWidth, window.ActualHeight);

            _viewModel.OnWindowBoundsChanged(this, new Views.WindowBoundsChangedEventArgs
            {
                Bounds = bounds
            });
        }
    }
}
