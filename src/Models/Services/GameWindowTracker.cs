using System.Diagnostics;
using System.Windows.Threading;
using TanukiTarkovMap.Core.Utils;
using TanukiTarkovMap.Models.Utils;

namespace TanukiTarkovMap.Models.Services;

public sealed record GameWindowState(bool CanTrack, bool IsRunning, bool IsGameForeground, bool IsAppForeground, IntPtr GameWindow);

public sealed class GameWindowTracker : IDisposable
{
    private readonly Dictionary<int, Process> _games = new();
    private readonly PInvoke.WinEventProc _callback;
    private readonly DispatcherTimer _timer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(2) };
    private readonly int _ownPid = Environment.ProcessId;
    private Dispatcher? _dispatcher;
    private IntPtr _hook;
    private IntPtr _gameWindow;
    private volatile bool _disposed;
    private bool _snapshotAvailable;
    private GameWindowState _state = new(false, false, false, false, IntPtr.Zero);
    public GameWindowState State => Volatile.Read(ref _state);
    public event EventHandler? StateChanged;
    public event EventHandler? GameExited;

    public GameWindowTracker()
    {
        _callback = OnForegroundChanged;
        _timer.Tick += OnTimer;
    }

    public void Start(Dispatcher dispatcher)
    {
        dispatcher.VerifyAccess();
        if (_dispatcher != null || _disposed) return;
        _dispatcher = dispatcher;
        try
        {
            _hook = PInvoke.SetWinEventHook(PInvoke.EVENT_SYSTEM_FOREGROUND, PInvoke.EVENT_SYSTEM_FOREGROUND,
                IntPtr.Zero, _callback, 0, 0, PInvoke.WINEVENT_OUTOFCONTEXT);
            if (_hook == IntPtr.Zero) Logger.SimpleLog("[GameWindowTracker] Foreground hook unavailable");
        }
        catch (Exception ex) { Logger.Error("[GameWindowTracker] Start failed", ex); }
        RefreshGame();
    }

    public void RefreshGame()
    {
        var dispatcher = _dispatcher;
        if (dispatcher == null || _disposed || dispatcher.HasShutdownStarted) return;
        if (!dispatcher.CheckAccess()) { dispatcher.BeginInvoke(RefreshGame); return; }
        var previous = State;
        try
        {
            ReadSnapshot();
            var foreground = PInvoke.GetForegroundWindow();
            PInvoke.GetWindowThreadProcessId(foreground, out var pid);
            var game = _snapshotAvailable && _games.ContainsKey(unchecked((int)pid));
            if (game) _gameWindow = foreground;
            var running = _games.Count > 0;
            if (!running) _gameWindow = IntPtr.Zero;
            Publish(new(_hook != IntPtr.Zero && _snapshotAvailable, running, game, pid == _ownPid, _gameWindow), previous);
        }
        catch (Exception ex)
        {
            _snapshotAvailable = false;
            Publish(previous with { CanTrack = false, IsGameForeground = false, IsAppForeground = false }, previous);
            Logger.Error("[GameWindowTracker] Refresh failed", ex);
        }
        if (State.IsRunning || (_hook != IntPtr.Zero && !_snapshotAvailable)) _timer.Start();
        else _timer.Stop();
    }

    private void ReadSnapshot()
    {
        var snapshot = Process.GetProcessesByName("EscapeFromTarkov");
        var live = new HashSet<int>();
        var complete = true;
        foreach (var process in snapshot)
        {
            var retained = false;
            try
            {
                var pid = process.Id;
                live.Add(pid);
                if (_games.ContainsKey(pid)) continue;
                _games[pid] = process;
                retained = true;
                try
                {
                    process.Exited += OnProcessExited;
                    process.EnableRaisingEvents = true;
                }
                catch (Exception ex) { Logger.Error("[GameWindowTracker] Exit notification unavailable", ex); }
                try
                {
                    if (process.MainWindowHandle != IntPtr.Zero) _gameWindow = process.MainWindowHandle;
                }
                catch (Exception ex) { Logger.Error("[GameWindowTracker] Initial game window unavailable", ex); }
            }
            catch (Exception ex)
            {
                complete = false;
                Logger.Error("[GameWindowTracker] Snapshot item failed", ex);
            }
            finally { if (!retained) process.Dispose(); }
        }
        foreach (var pid in _games.Keys.Where(pid => complete && !live.Contains(pid)).ToArray())
        {
            var process = _games[pid];
            process.Exited -= OnProcessExited;
            process.Dispose();
            _games.Remove(pid);
        }
        _snapshotAvailable = complete;
    }

    private void Publish(GameWindowState next, GameWindowState previous)
    {
        Volatile.Write(ref _state, next);
        try { if (next != previous) StateChanged?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { Logger.Error("[GameWindowTracker] State subscriber failed", ex); }
        try { if (_snapshotAvailable && previous.IsRunning && !next.IsRunning) GameExited?.Invoke(this, EventArgs.Empty); }
        catch (Exception ex) { Logger.Error("[GameWindowTracker] Exit subscriber failed", ex); }
    }

    private void OnForegroundChanged(IntPtr hook, uint evt, IntPtr window, int obj, int child, uint thread, uint time) => RefreshGame();
    private void OnTimer(object? sender, EventArgs e) => RefreshGame();

    private void OnProcessExited(object? sender, EventArgs e)
    {
        var dispatcher = _dispatcher;
        if (_disposed || dispatcher == null || dispatcher.HasShutdownStarted) return;
        dispatcher.BeginInvoke(RefreshGame);
    }

    public void Dispose()
    {
        if (_dispatcher != null && !_dispatcher.CheckAccess())
        {
            if (!_dispatcher.HasShutdownStarted) _dispatcher.Invoke(Dispose);
            return;
        }
        if (_disposed) return;
        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimer;
        if (_hook != IntPtr.Zero && !PInvoke.UnhookWinEvent(_hook))
            Logger.SimpleLog("[GameWindowTracker] Foreground unhook failed");
        _hook = IntPtr.Zero;
        foreach (var process in _games.Values)
        {
            process.Exited -= OnProcessExited;
            process.Dispose();
        }
        _games.Clear();
    }
}
