using System.Collections.ObjectModel;
using TanukiTarkovMap.Core.Offline;
using System.Windows;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using TanukiTarkovMap.Core.Site;
using TanukiTarkovMap.Core.Data;
using TanukiTarkovMap.Models.Services;
using TanukiTarkovMap.Core.Services;
using TanukiTarkovMap.Models.Utils;
using TanukiTarkovMap.Core.Utils;

namespace TanukiTarkovMap.ViewModels;

public partial class MapOverlayViewModel : ObservableObject
{
    private readonly OnlineOverlay _state;
    private readonly HashSet<string> _pinged = [];
    private string? _map, _failure, _teammateFailure;
    private IReadOnlyList<SharedTeammate> _teammates = [];

    [ObservableProperty] private bool _isEnabled;
    [ObservableProperty] private bool _isMarkerVisible;
    [ObservableProperty] private bool _showNotice;
    [ObservableProperty] private Rect _clip;
    [ObservableProperty] private double _markerX;
    [ObservableProperty] private double _markerY;
    [ObservableProperty] private double _direction;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Radius), nameof(ArrowScale), nameof(ArrowLeft), nameof(ArrowTop), nameof(LabelTop))]
    private double _diameter;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Radius), nameof(ArrowTop), nameof(LabelTop))]
    private double _stroke;
    [ObservableProperty] private int _ping;

    public double Radius => Math.Max(0, (Diameter - Stroke) / 2);
    private double ArrowSize => 25 * Diameter / 20;
    public double ArrowScale => ArrowSize / 100;
    public double ArrowLeft => -ArrowSize / 2;
    public double ArrowTop => -ArrowSize * 0.75 - Diameter * 0.4 - Stroke / 2;
    public double LabelTop => -(ArrowTop + ArrowSize * 0.05) + 2;

    public ObservableCollection<TeammateMarker> Teammates { get; } = [];

    public MapOverlayViewModel(Func<SiteScript, Task<bool>> execute)
    {
        _state = new OnlineOverlay(execute, MapResources.ReadMetadata);
        _state.Changed += Render;
        SetZoom(100);
    }

    public void SetContext(bool enabled, string? map, int token)
    {
        if (_map != map) { _map = map; _pinged.Clear(); }
        _state.SetContext(enabled, map, token);
    }

    public void SetZoom(int percent)
    {
        var scale = Math.Max(1, percent / 100.0);
        Diameter = 20 * scale;
        Stroke = 2 * scale;
        _state.SetZoom(percent);
    }

    public void SetScreenshot(string filename) => _state.SetScreenshot(filename);
    public void Accept(string json) => _state.Accept(json);
    public Task MaintainAsync(SiteControl site) => site.MaintainOverlayAsync(_state);

    public void SetTeammates(IReadOnlyList<SharedTeammate> teammates)
    {
        _teammates = teammates;
        RenderTeammates();
    }

    private void Render()
    {
        IsEnabled = _state.IsEnabled;
        IsMarkerVisible = _state.IsMarkerVisible;
        var clip = _state.Clip;
        Clip = clip.Width > 0 && clip.Height > 0 ? new(clip.X, clip.Y, clip.Width, clip.Height) : Rect.Empty;
        MarkerX = _state.Marker.X;
        MarkerY = _state.Marker.Y;
        Direction = _state.Direction;
        ShowNotice = IsEnabled && _state.Failure != null;
        if (_failure != _state.Failure)
        {
            _failure = _state.Failure;
            if (IsEnabled) Logger.SimpleLog($"[MapOverlay] {_failure ?? "ready"}");
        }
        if (IsMarkerVisible && Clip.Contains(new Point(MarkerX, MarkerY))
            && _state.Filename is { } filename && _pinged.Add(filename))
        {
            Ping++;
            Logger.SimpleLog($"[MapOverlay] Position rendered (screenshot: {filename})");
        }
        RenderTeammates();
    }

    private void RenderTeammates()
    {
        try
        {
            var shown = new HashSet<string>();
            foreach (var teammate in _teammates)
            {
                if (teammate.Position.Map != _map
                    || !_state.TryProject(teammate.Position.ToProjection(), out var point, out var direction)) continue;
                shown.Add(teammate.MemberId);
                var marker = Teammates.FirstOrDefault(existing => existing.MemberId == teammate.MemberId);
                if (marker == null) Teammates.Add(marker = new TeammateMarker(this, teammate.MemberId));
                marker.Nickname = teammate.Name;
                marker.Fill = TeammateMarker.BrushFor(teammate.Slot);
                marker.X = point.X;
                marker.Y = point.Y;
                marker.Direction = direction;
            }
            for (var index = Teammates.Count - 1; index >= 0; index--)
                if (!shown.Contains(Teammates[index].MemberId)) Teammates.RemoveAt(index);
            _teammateFailure = null;
        }
        catch (Exception ex)
        {
            if (_teammateFailure != ex.Message) Logger.SimpleLog($"[MapOverlay] Teammates not rendered: {ex.Message}");
            _teammateFailure = ex.Message;
        }
    }
}

public partial class TeammateMarker(MapOverlayViewModel overlay, string memberId) : ObservableObject
{
    private static readonly Brush[] SlotBrushes = MarkerColors.Teammates.Select(color =>
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return (Brush)brush;
    }).ToArray();

    public static Brush BrushFor(int slot) => SlotBrushes[((slot % SlotBrushes.Length) + SlotBrushes.Length) % SlotBrushes.Length];

    public MapOverlayViewModel Overlay { get; } = overlay;
    public string MemberId { get; } = memberId;

    [ObservableProperty] private string _nickname = string.Empty;
    [ObservableProperty] private Brush _fill = BrushFor(0);
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _direction;
}
