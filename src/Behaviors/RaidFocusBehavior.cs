using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.ViewModels;

namespace TanukiTarkovMap.Behaviors;

public sealed class RaidFocusBehavior : Behavior<Window>
{
    public static readonly DependencyProperty IsActiveProperty = DependencyProperty.Register(nameof(IsActive), typeof(bool),
        typeof(RaidFocusBehavior), new PropertyMetadata(false, (obj, _) => ((RaidFocusBehavior)obj).UpdateSession()));
    public bool IsActive { get => (bool)GetValue(IsActiveProperty); set => SetValue(IsActiveProperty, value); }

    private const int PauseEntryGraceMs = 3000;

    private readonly DispatcherTimer _timer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(33) };
    private readonly DispatcherTimer _hintTimer = new() { Interval = TimeSpan.FromMilliseconds(TopBarAnimationBehavior.HideDelayMs) };
    private readonly RaidFocusPause _pause = new(TopBarAnimationBehavior.HideDelayMs, PauseEntryGraceMs);
    private MainWindowViewModel? _viewModel;
    private TopBarAnimationBehavior? _topBar;
    private IntPtr _handle;
    private PixelRect _home;
    private PixelRect _last;
    private bool _session;
    private bool _ready;
    private bool _restorePending;
    private int _centerSamples;

    protected override void OnAttached()
    {
        AssociatedObject.Loaded += OnLoaded;
        AssociatedObject.Closed += OnClosed;
        AssociatedObject.StateChanged += OnStateChanged;
        AssociatedObject.MouseEnter += OnMouseEnter;
        _timer.Tick += OnTick;
        _hintTimer.Tick += OnHintElapsed;
    }

    protected override void OnDetaching()
    {
        StopSession();
        if (_viewModel != null) _viewModel.WindowBoundsChangedByUser -= OnUserBoundsChanged;
        AssociatedObject.Loaded -= OnLoaded;
        AssociatedObject.Closed -= OnClosed;
        AssociatedObject.StateChanged -= OnStateChanged;
        AssociatedObject.MouseEnter -= OnMouseEnter;
        _timer.Tick -= OnTick;
        _hintTimer.Tick -= OnHintElapsed;
        _ready = false;
        base.OnDetaching();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        if (_ready) return;
        _viewModel = AssociatedObject.DataContext as MainWindowViewModel;
        _topBar = Interaction.GetBehaviors(AssociatedObject).OfType<TopBarAnimationBehavior>().FirstOrDefault();
        _handle = new WindowInteropHelper(AssociatedObject).Handle;
        if (_viewModel != null) _viewModel.WindowBoundsChangedByUser += OnUserBoundsChanged;
        AssociatedObject.Dispatcher.BeginInvoke(() => { _ready = true; UpdateSession(); }, DispatcherPriority.Loaded);
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _timer.Stop();
        _hintTimer.Stop();
        if (_viewModel != null) _viewModel.WindowBoundsChangedByUser -= OnUserBoundsChanged;
        _ready = false;
    }

    private void UpdateSession()
    {
        if (!_ready || _viewModel == null) return;
        try
        {
            if (!IsActive) { StopSession(); return; }
            if (_session || _restorePending || AssociatedObject.WindowState == WindowState.Minimized || !TryRect(out _home)) return;
            _last = _home;
            _pause.Reset(AltDown());
            _centerSamples = 0;
            _session = true;
            _viewModel.RaidFocusHomePosition = new(AssociatedObject.Left, AssociatedObject.Top);
            _timer.Start();
        }
        catch (Exception ex) { Logger.Error("[RaidFocus] Session update failed", ex); }
    }

    private void StopSession()
    {
        _timer.Stop();
        _pause.Reset(AltDown());
        HideHint();
        if (!_session || _viewModel == null) return;
        if (AssociatedObject.WindowState == WindowState.Minimized)
        {
            _session = false;
            _restorePending = true;
            return;
        }
        try
        {
            if (TryRect(out var current) && (current.Left != _home.Left || current.Top != _home.Top)) Move(_home);
        }
        finally
        {
            _session = false;
            _viewModel.RaidFocusHomePosition = null;
        }
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        if (AssociatedObject.WindowState == WindowState.Minimized) return;
        if (_restorePending && _viewModel != null)
        {
            Move(_home);
            _restorePending = false;
            _viewModel.RaidFocusHomePosition = null;
        }
        UpdateSession();
    }

    private void OnUserBoundsChanged(object? sender, EventArgs e)
    {
        if (!_session || _viewModel == null || AssociatedObject.WindowState == WindowState.Minimized || !TryRect(out var current)) return;
        if (current.Left != _last.Left || current.Top != _last.Top)
        {
            _home = current;
            _viewModel.RaidFocusHomePosition = new(AssociatedObject.Left, AssociatedObject.Top);
        }
        _last = current;
    }

    private void OnMouseEnter(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_session) Evaluate();
    }

    private void OnTick(object? sender, EventArgs e) => Evaluate();

    private void Evaluate()
    {
        try
        {
            var now = Environment.TickCount64;
            var game = ServiceLocator.GameWindowTracker.State;
            var info = new CURSORINFO { cbSize = Marshal.SizeOf<CURSORINFO>() };
            if (!PInvoke.GetCursorInfo(ref info) || !TryRect(out var window)) return;
            int x = info.ptScreenPos.X, y = info.ptScreenPos.Y;
            var visible = (info.flags & PInvoke.CURSOR_SHOWING) != 0 && info.hCursor != IntPtr.Zero;
            var inside = window.Contains(x, y);
            if (_pause.Update(now, AltDown(), inside) == RaidFocusHint.Paused) ShowHint(RaidFocusHint.Paused);

            var minimized = AssociatedObject.WindowState == WindowState.Minimized;
            if (RaidFocusPolicy.ShouldDodge(game.IsGameForeground, AssociatedObject.IsVisible, minimized, visible, _pause.IsPaused, inside))
            {
                if (TryGameMonitor(game.GameWindow, out var monitor) && RaidFocusPolicy.Dodge(window, monitor, x, y) is { } target)
                {
                    Move(target);
                    _pause.Dodged(now);
                    _centerSamples = 0;
                    _topBar?.HideAfterDodge();
                    ShowHint(RaidFocusHint.PausePrompt);
                }
                return;
            }

            var away = window.Left != _home.Left || window.Top != _home.Top;
            var nearCenter = away && TryGameClientArea(game.GameWindow, out var area) && RaidFocusPolicy.NearCenter(area, x, y);
            _centerSamples = nearCenter ? _centerSamples + 1 : 0;
            if (!minimized && RaidFocusPolicy.ShouldReturn(away, _pause.IsPaused, game.IsGameForeground, _centerSamples, visible, _home.Contains(x, y)))
            {
                Move(_home);
                _centerSamples = 0;
            }
        }
        catch (Exception ex)
        {
            _timer.Stop();
            Logger.Error("[RaidFocus] Cursor check failed", ex);
            StopSession();
        }
    }

    private static bool AltDown() => (PInvoke.GetAsyncKeyState(PInvoke.VK_MENU) & 0x8000) != 0;

    private bool TryGameMonitor(IntPtr gameWindow, out PixelRect monitor)
    {
        monitor = default;
        var mine = PInvoke.MonitorFromWindow(_handle, PInvoke.MONITOR_DEFAULTTONEAREST);
        if (gameWindow == IntPtr.Zero || mine == IntPtr.Zero || mine != PInvoke.MonitorFromWindow(gameWindow, PInvoke.MONITOR_DEFAULTTONEAREST)) return false;
        var monitorInfo = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
        if (!PInvoke.GetMonitorInfo(mine, ref monitorInfo)) return false;
        var rect = monitorInfo.rcMonitor;
        monitor = new(rect.Left, rect.Top, rect.Right, rect.Bottom);
        return true;
    }

    private static bool TryGameClientArea(IntPtr gameWindow, out PixelRect area)
    {
        area = default;
        if (gameWindow == IntPtr.Zero || !PInvoke.GetClientRect(gameWindow, out var client)) return false;
        var origin = new POINT();
        if (!PInvoke.ClientToScreen(gameWindow, ref origin)) return false;
        area = new(origin.X, origin.Y, origin.X + client.Width, origin.Y + client.Height);
        return area.Width > 0 && area.Height > 0;
    }

    private void ShowHint(RaidFocusHint hint)
    {
        if (_viewModel == null) return;
        _viewModel.RaidFocusHint = hint;
        _viewModel.IsRaidFocusHintVisible = true;
        _hintTimer.Stop();
        _hintTimer.Start();
    }

    private void HideHint()
    {
        _hintTimer.Stop();
        if (_viewModel != null) _viewModel.IsRaidFocusHintVisible = false;
    }

    private void OnHintElapsed(object? sender, EventArgs e) => HideHint();

    private bool TryRect(out PixelRect rect)
    {
        var found = PInvoke.GetWindowRect(_handle, out var native);
        rect = new(native.Left, native.Top, native.Right, native.Bottom);
        return found;
    }

    private void Move(PixelRect target)
    {
        if (_viewModel == null) return;
        _viewModel.IsRaidFocusMove = true;
        try
        {
            if (!PInvoke.SetWindowPos(_handle, 0, target.Left, target.Top, 0, 0,
                PInvoke.SWP_NOSIZE | PInvoke.SWP_NOZORDER | PInvoke.SWP_NOACTIVATE))
                Logger.SimpleLog("[RaidFocus] Window move failed");
            if (TryRect(out var current)) _last = current;
        }
        finally { _viewModel.IsRaidFocusMove = false; }
    }
}
