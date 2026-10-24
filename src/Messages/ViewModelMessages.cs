using CommunityToolkit.Mvvm.Messaging.Messages;
using TanukiTarkovMap.Core.Data;

namespace TanukiTarkovMap.Messages
{
    public class MapSelectionChangedMessage : ValueChangedMessage<MapInfo?>
    {
        public MapSelectionChangedMessage(MapInfo? value) : base(value) { }
    }

    public class HideWebElementsChangedMessage : ValueChangedMessage<bool>
    {
        public HideWebElementsChangedMessage(bool value) : base(value) { }
    }

    public class ZoomLevelChangedMessage : ValueChangedMessage<int>
    {
        public ZoomLevelChangedMessage(int value) : base(value) { }
    }

    public class ViewModeChangedMessage : ValueChangedMessage<MapViewMode>
    {
        public ViewModeChangedMessage(MapViewMode value) : base(value) { }
    }

    public class ViewModeSelectorChangedMessage
    {
    }

    public class HotkeySettingsChangedMessage
    {
    }

    public class ExtractionFilterChangedMessage : ValueChangedMessage<bool>
    {
        public ExtractionFilterChangedMessage(bool value) : base(value) { }
    }

    public class TopBarHiddenChangedMessage : ValueChangedMessage<bool>
    {
        public TopBarHiddenChangedMessage(bool value) : base(value) { }
    }

    public class OpacitySliderDragMessage : ValueChangedMessage<bool>
    {
        public OpacitySliderDragMessage(bool value) : base(value) { }
    }

    public class NavigateToUrlMessage : ValueChangedMessage<string>
    {
        public NavigateToUrlMessage(string url) : base(url) { }
    }

    public class MonitorRefreshRateChangedMessage : ValueChangedMessage<int>
    {
        public MonitorRefreshRateChangedMessage(int value) : base(value) { }
    }

    public class UpdateReadyMessage : ValueChangedMessage<string>
    {
        public UpdateReadyMessage(string value) : base(value) { }
    }

    public class SettingsOpenedMessage
    {
    }

    public class LanguageChangedMessage : ValueChangedMessage<string>
    {
        public LanguageChangedMessage(string value) : base(value) { }
    }

    public class UpdateIconPreviewMessage : ValueChangedMessage<bool>
    {
        public UpdateIconPreviewMessage(bool value) : base(value) { }
    }
}
