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
}
