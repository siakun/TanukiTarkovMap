using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors
{

    public class WindowStateBehavior : Behavior<Window>
    {
        private MainWindowViewModel? _viewModel;
        private Grid? _mainGrid;
        private Button? _maximizeRestoreButton;
        private FrameworkElement? _browserHost;
        private HwndSource? _hwndSource;
        private bool _browserSizeFrozen;
        private DispatcherTimer? _browserRefreshTimer;
        private Border? _paintedContainer;
        private object? _containerBackground;
        private bool _sizeMoveTracking;
        private POINT _sizeMoveStartCursor;
        private RECT _sizeMoveStartRect;
        private bool _isClampingLocation = false;
        private bool _isInitializing = true;

        private static readonly TimeSpan FrozenBrowserRefreshInterval = TimeSpan.FromMilliseconds(250);

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

        public static readonly DependencyProperty BrowserHostNameProperty =
            DependencyProperty.Register(
                nameof(BrowserHostName),
                typeof(string),
                typeof(WindowStateBehavior),
                new PropertyMetadata("WebBrowser"));

        public string BrowserHostName
        {
            get => (string)GetValue(BrowserHostNameProperty);
            set => SetValue(BrowserHostNameProperty, value);
        }

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

            _hwndSource?.RemoveHook(WndProc);
            _hwndSource = null;
            _browserRefreshTimer?.Stop();
            AssociatedObject.Loaded -= OnWindowLoaded;
            AssociatedObject.StateChanged -= OnStateChanged;
            AssociatedObject.LocationChanged -= OnLocationChanged;
            AssociatedObject.SizeChanged -= OnSizeChanged;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            _viewModel = AssociatedObject.DataContext as MainWindowViewModel;

            _mainGrid = AssociatedObject.FindName(MainGridName) as Grid;
            _maximizeRestoreButton = AssociatedObject.FindName(MaximizeRestoreButtonName) as Button;
            _browserHost = AssociatedObject.FindName(BrowserHostName) as FrameworkElement;
            _hwndSource = HwndSource.FromHwnd(new WindowInteropHelper(AssociatedObject).Handle);
            _hwndSource?.AddHook(WndProc);

            if (_mainGrid == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MainGrid '{MainGridName}' not found");
            }

            if (_maximizeRestoreButton == null)
            {
                Logger.SimpleLog($"[WindowStateBehavior] MaximizeRestoreButton '{MaximizeRestoreButtonName}' not found");
            }

            AssociatedObject.Dispatcher.InvokeAsync(() =>
            {
                _isInitializing = false;
                Logger.SimpleLog("[WindowStateBehavior] Initialization complete");
            }, System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void OnStateChanged(object? sender, EventArgs e)
        {
            var window = AssociatedObject;

            if (window.WindowState == WindowState.Maximized)
            {
                if (_mainGrid != null)
                {
                    var thickness = SystemParameters.WindowResizeBorderThickness;
                    _mainGrid.Margin = new Thickness(
                        thickness.Left,
                        thickness.Top,
                        thickness.Right,
                        thickness.Bottom);
                }

                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "❐";
                }
            }
            else
            {
                if (_mainGrid != null)
                {
                    _mainGrid.Margin = new Thickness(0);
                }

                if (_maximizeRestoreButton != null)
                {
                    _maximizeRestoreButton.Content = "□";
                }
            }
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case PInvoke.WM_ENTERSIZEMOVE:
                    _sizeMoveTracking = PInvoke.GetCursorPos(out _sizeMoveStartCursor)
                        && PInvoke.GetWindowRect(hwnd, out _sizeMoveStartRect);
                    break;
                case PInvoke.WM_MOVING:
                    if (FollowCursorWhileMoving(lParam))
                    {
                        handled = true;
                        return (IntPtr)1;
                    }
                    break;
                case PInvoke.WM_SIZING:
                    FreezeBrowserSize();
                    if (FollowCursorWhileSizing(wParam.ToInt32(), lParam))
                    {
                        handled = true;
                        return (IntPtr)1;
                    }
                    break;
                case PInvoke.WM_EXITSIZEMOVE:
                    _sizeMoveTracking = false;
                    ReleaseBrowserSize();
                    break;
            }
            return IntPtr.Zero;
        }

        private bool FollowCursorWhileMoving(IntPtr rectPointer)
        {
            if (!_sizeMoveTracking || !PInvoke.GetCursorPos(out var cursor)) return false;

            var proposed = Marshal.PtrToStructure<RECT>(rectPointer);
            if (proposed.Width != _sizeMoveStartRect.Width || proposed.Height != _sizeMoveStartRect.Height)
            {
                _sizeMoveStartRect = proposed;
                _sizeMoveStartCursor = cursor;
                return false;
            }

            int left = _sizeMoveStartRect.Left + cursor.X - _sizeMoveStartCursor.X;
            int top = _sizeMoveStartRect.Top + cursor.Y - _sizeMoveStartCursor.Y;
            if (left == proposed.Left && top == proposed.Top) return false;

            Marshal.StructureToPtr(new RECT
            {
                Left = left,
                Top = top,
                Right = left + proposed.Width,
                Bottom = top + proposed.Height
            }, rectPointer, false);
            return true;
        }

        private bool FollowCursorWhileSizing(int edge, IntPtr rectPointer)
        {
            if (!_sizeMoveTracking || !PInvoke.GetCursorPos(out var cursor)) return false;

            var proposed = Marshal.PtrToStructure<RECT>(rectPointer);
            int dx = cursor.X - _sizeMoveStartCursor.X;
            int dy = cursor.Y - _sizeMoveStartCursor.Y;

            var dpi = VisualTreeHelper.GetDpi(AssociatedObject);
            int minWidth = (int)Math.Ceiling(AssociatedObject.MinWidth * dpi.DpiScaleX);
            int minHeight = (int)Math.Ceiling(AssociatedObject.MinHeight * dpi.DpiScaleY);

            var corrected = proposed;
            if (edge is PInvoke.WMSZ_LEFT or PInvoke.WMSZ_TOPLEFT or PInvoke.WMSZ_BOTTOMLEFT)
                corrected.Left = Math.Min(_sizeMoveStartRect.Left + dx, corrected.Right - minWidth);
            if (edge is PInvoke.WMSZ_RIGHT or PInvoke.WMSZ_TOPRIGHT or PInvoke.WMSZ_BOTTOMRIGHT)
                corrected.Right = Math.Max(_sizeMoveStartRect.Right + dx, corrected.Left + minWidth);
            if (edge is PInvoke.WMSZ_TOP or PInvoke.WMSZ_TOPLEFT or PInvoke.WMSZ_TOPRIGHT)
                corrected.Top = Math.Min(_sizeMoveStartRect.Top + dy, corrected.Bottom - minHeight);
            if (edge is PInvoke.WMSZ_BOTTOM or PInvoke.WMSZ_BOTTOMLEFT or PInvoke.WMSZ_BOTTOMRIGHT)
                corrected.Bottom = Math.Max(_sizeMoveStartRect.Bottom + dy, corrected.Top + minHeight);

            if (corrected.Left == proposed.Left && corrected.Top == proposed.Top
                && corrected.Right == proposed.Right && corrected.Bottom == proposed.Bottom) return false;

            Marshal.StructureToPtr(corrected, rectPointer, false);
            return true;
        }

        private void FreezeBrowserSize()
        {
            if (_browserSizeFrozen || _browserHost == null || _browserHost.ActualWidth <= 0 || _browserHost.ActualHeight <= 0) return;
            _browserSizeFrozen = true;
            _browserHost.Width = _browserHost.ActualWidth;
            _browserHost.Height = _browserHost.ActualHeight;
            _browserHost.HorizontalAlignment = HorizontalAlignment.Left;
            _browserHost.VerticalAlignment = VerticalAlignment.Top;
            PaintContainerWithMapBackground();

            if (_browserRefreshTimer == null)
            {
                _browserRefreshTimer = new DispatcherTimer(DispatcherPriority.Normal, AssociatedObject.Dispatcher)
                {
                    Interval = FrozenBrowserRefreshInterval
                };
                _browserRefreshTimer.Tick += OnBrowserRefreshTick;
            }
            _browserRefreshTimer.Start();
        }

        private void OnBrowserRefreshTick(object? sender, EventArgs e)
        {
            if (!_browserSizeFrozen || _browserHost == null) return;

            var slot = LayoutInformation.GetLayoutSlot(_browserHost);
            var margin = _browserHost.Margin;
            double width = Math.Max(1, slot.Width - margin.Left - margin.Right);
            double height = Math.Max(1, slot.Height - margin.Top - margin.Bottom);
            if (Math.Abs(width - _browserHost.Width) < 1 && Math.Abs(height - _browserHost.Height) < 1) return;

            _browserHost.Width = width;
            _browserHost.Height = height;
        }

        private void ReleaseBrowserSize()
        {
            _browserRefreshTimer?.Stop();
            if (!_browserSizeFrozen || _browserHost == null) return;
            _browserSizeFrozen = false;
            _browserHost.ClearValue(FrameworkElement.WidthProperty);
            _browserHost.ClearValue(FrameworkElement.HeightProperty);
            _browserHost.ClearValue(FrameworkElement.HorizontalAlignmentProperty);
            _browserHost.ClearValue(FrameworkElement.VerticalAlignmentProperty);
            RestoreContainerBackground();
        }

        private void PaintContainerWithMapBackground()
        {
            if (_browserHost?.Parent is not Border container) return;
            var localBackground = container.ReadLocalValue(Border.BackgroundProperty);
            if (localBackground != DependencyProperty.UnsetValue && localBackground is not Brush) return;
            if (SampleMapBackground(_browserHost) is not Color mapBackground) return;

            _paintedContainer = container;
            _containerBackground = localBackground;
            container.Background = new SolidColorBrush(mapBackground);
        }

        private void RestoreContainerBackground()
        {
            if (_paintedContainer == null) return;
            if (_containerBackground == DependencyProperty.UnsetValue) _paintedContainer.ClearValue(Border.BackgroundProperty);
            else _paintedContainer.Background = (Brush?)_containerBackground;
            _paintedContainer = null;
            _containerBackground = null;
        }

        private static Color? SampleMapBackground(DependencyObject host)
        {
            if (FindBrowserBitmap(host) is not BitmapSource bitmap
                || (bitmap.Format != PixelFormats.Pbgra32 && bitmap.Format != PixelFormats.Bgra32)
                || bitmap.PixelWidth < 1 || bitmap.PixelHeight < 1) return null;

            var column = new int[bitmap.PixelHeight];
            bitmap.CopyPixels(new Int32Rect(bitmap.PixelWidth - 1, 0, 1, bitmap.PixelHeight), column, sizeof(int), 0);

            var counts = new Dictionary<int, int>();
            int dominant = 0, dominantCount = 0;
            foreach (int pixel in column)
            {
                if ((uint)pixel >> 24 != 0xFF) continue;
                int count = counts.GetValueOrDefault(pixel) + 1;
                counts[pixel] = count;
                if (count > dominantCount) (dominant, dominantCount) = (pixel, count);
            }
            if (dominantCount == 0) return null;
            return Color.FromRgb((byte)(dominant >> 16), (byte)(dominant >> 8), (byte)dominant);
        }

        private static BitmapSource? FindBrowserBitmap(DependencyObject parent)
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Image { Source: BitmapSource bitmap }) return bitmap;
                if (FindBrowserBitmap(child) is BitmapSource found) return found;
            }
            return null;
        }

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

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_isClampingLocation) return;
            if (_isInitializing) return;

            NotifyWindowBoundsChanged();
        }

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
