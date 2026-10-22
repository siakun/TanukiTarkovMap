namespace TanukiTarkovMap.Models.Utils;

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;
    public bool Contains(int x, int y) => x >= Left && x < Right && y >= Top && y < Bottom;
}

public enum RaidFocusHint { None, PausePrompt, Paused }

public static class RaidFocusPolicy
{
    public const int ReturnCenterTolerance = 4;
    public const int ReturnCenterSamples = 3;

    public static bool ShouldDodge(bool gameForeground, bool windowVisible, bool minimized, bool cursorVisible, bool paused, bool inside)
        => gameForeground && windowVisible && !minimized && cursorVisible && !paused && inside;

    public static bool NearCenter(PixelRect area, int x, int y)
        => Math.Abs(x - (area.Left + area.Right) / 2) <= ReturnCenterTolerance
           && Math.Abs(y - (area.Top + area.Bottom) / 2) <= ReturnCenterTolerance;

    public static bool ShouldReturn(bool away, bool paused, bool gameForeground, int centerSamples, bool cursorVisible, bool insideHome)
        => away && !paused && gameForeground && centerSamples >= ReturnCenterSamples && !(cursorVisible && insideHome);

    public static PixelRect? Dodge(PixelRect window, PixelRect monitor, int x, int y)
    {
        if (window.Width <= 0 || window.Width > monitor.Width) return null;
        var left = monitor.Left + monitor.Right - window.Right;
        left = Math.Clamp(left, monitor.Left, monitor.Right - window.Width);
        var target = new PixelRect(left, window.Top, left + window.Width, window.Bottom);
        if (target.Contains(x, y))
        {
            left = (long)x * 2 < (long)monitor.Left + monitor.Right ? monitor.Right - window.Width : monitor.Left;
            target = new(left, window.Top, left + window.Width, window.Bottom);
        }
        return target.Contains(x, y) || target.Left == window.Left ? null : target;
    }
}

public sealed class RaidFocusPause(int hintMs, int entryGraceMs)
{
    private long _promptUntil = long.MinValue;
    private long _pausedAt;
    private long? _leftAt;
    private bool _entered;
    private bool _wasInside;
    private bool _altWasDown;

    public bool IsPaused { get; private set; }

    public void Dodged(long now) => _promptUntil = now + hintMs;

    public RaidFocusHint Update(long now, bool altDown, bool inside)
    {
        var altPressed = altDown && !_altWasDown;
        _altWasDown = altDown;
        var entered = inside && !_wasInside;
        var left = !inside && _wasInside;
        _wasInside = inside;

        if (!IsPaused)
        {
            if (!altPressed || now >= _promptUntil) return RaidFocusHint.None;
            IsPaused = true;
            _promptUntil = long.MinValue;
            _pausedAt = now;
            _entered = inside;
            _leftAt = null;
            return inside ? RaidFocusHint.Paused : RaidFocusHint.None;
        }

        if (entered)
        {
            _entered = true;
            _leftAt = null;
            return RaidFocusHint.Paused;
        }
        if (left) _leftAt = now;
        if (!inside && (_entered ? _leftAt is { } leftAt && now - leftAt >= hintMs : now - _pausedAt >= entryGraceMs))
            IsPaused = false;
        return RaidFocusHint.None;
    }

    public void Reset(bool altDown)
    {
        IsPaused = false;
        _promptUntil = long.MinValue;
        _leftAt = null;
        _entered = false;
        _wasInside = false;
        _altWasDown = altDown;
    }
}
