using CommunityToolkit.Mvvm.Messaging.Messages;
using TanukiTarkovMap.Models.Data;

namespace TanukiTarkovMap.Messages
{
    /// <summary>
    /// 맵 선택 변경 메시지 (MainWindowViewModel → WebBrowserViewModel)
    /// </summary>
    public class MapSelectionChangedMessage : ValueChangedMessage<MapInfo?>
    {
        public MapSelectionChangedMessage(MapInfo? value) : base(value) { }
    }

    /// <summary>
    /// UI 요소 숨기기 설정 변경 메시지 (MainWindowViewModel → WebBrowserViewModel)
    /// </summary>
    public class HideWebElementsChangedMessage : ValueChangedMessage<bool>
    {
        public HideWebElementsChangedMessage(bool value) : base(value) { }
    }

    /// <summary>
    /// 줌 레벨 변경 메시지 (MainWindowViewModel → WebBrowserViewModel)
    /// </summary>
    public class ZoomLevelChangedMessage : ValueChangedMessage<int>
    {
        public ZoomLevelChangedMessage(int value) : base(value) { }
    }

    /// <summary>
    /// 맵 수신 메시지 (WebBrowserViewModel → MainWindowViewModel)
    /// </summary>
    public class MapReceivedMessage : ValueChangedMessage<string>
    {
        public MapReceivedMessage(string value) : base(value) { }
    }

    /// <summary>
    /// Pilot 연결 메시지 (WebBrowserViewModel → MainWindowViewModel)
    /// </summary>
    public class PilotConnectedMessage
    {
    }

    /// <summary>
    /// 핫키 설정 변경 메시지 (SettingsViewModel → MainWindow)
    /// </summary>
    public class HotkeySettingsChangedMessage
    {
    }

    /// <summary>
    /// Extraction 필터 변경 메시지 (MainWindowViewModel → WebBrowserViewModel)
    /// true = PMC, false = SCAV
    /// </summary>
    public class ExtractionFilterChangedMessage : ValueChangedMessage<bool>
    {
        public ExtractionFilterChangedMessage(bool value) : base(value) { }
    }

    /// <summary>
    /// TopBar 숨김 상태 변경 메시지 (TopBarAnimationBehavior → MainWindowViewModel)
    /// true = 숨김, false = 보임
    /// </summary>
    public class TopBarHiddenChangedMessage : ValueChangedMessage<bool>
    {
        public TopBarHiddenChangedMessage(bool value) : base(value) { }
    }

    /// <summary>
    /// 투명도 슬라이더 드래그 상태 메시지 (OpacitySliderDragBehavior → MainWindowViewModel)
    /// true = 드래그 시작, false = 드래그 종료
    /// </summary>
    public class OpacitySliderDragMessage : ValueChangedMessage<bool>
    {
        public OpacitySliderDragMessage(bool value) : base(value) { }
    }
}
