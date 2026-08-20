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
    /// 로컬 맵 기능 사용 여부 변경 메시지 (SettingsViewModel → MainWindowViewModel)
    /// 상단바의 Online/Local 전환을 보일지 말지가 이 값에 달려 있다
    /// </summary>
    public class LocalMapFeatureChangedMessage : ValueChangedMessage<bool>
    {
        public LocalMapFeatureChangedMessage(bool value) : base(value) { }
    }

    /// <summary>
    /// 로컬 맵 전환 메시지 (MainWindowViewModel → WebBrowserViewModel)
    /// true면 사본으로 응답하고, false면 사이트를 그대로 연다
    /// </summary>
    public class LocalMapModeChangedMessage : ValueChangedMessage<bool>
    {
        public LocalMapModeChangedMessage(bool value) : base(value) { }
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

    /// <summary>
    /// URL 이동 메시지 (SettingsViewModel → WebBrowserViewModel)
    /// 디버그 모드로 지정된 URL로 이동
    /// </summary>
    public class NavigateToUrlMessage : ValueChangedMessage<string>
    {
        public NavigateToUrlMessage(string url) : base(url) { }
    }

    /// <summary>
    /// 모니터 주사율 변경 메시지 (MonitorRefreshRateBehavior → WebBrowserViewModel)
    /// 값 = 창이 위치한 모니터의 현재 주사율(Hz)
    /// </summary>
    public class MonitorRefreshRateChangedMessage : ValueChangedMessage<int>
    {
        public MonitorRefreshRateChangedMessage(int value) : base(value) { }
    }

    /// <summary>
    /// 업데이트 다운로드 완료 메시지 (UpdateService → MainWindowViewModel)
    /// 값 = 적용 대기 중인 새 버전 문자열
    /// </summary>
    public class UpdateReadyMessage : ValueChangedMessage<string>
    {
        public UpdateReadyMessage(string value) : base(value) { }
    }

    /// <summary>
    /// 설정 오버레이 열림 메시지 (MainWindowViewModel → SettingsViewModel)
    /// 버전 목록처럼 화면을 볼 때만 필요한 네트워크 조회를 이 시점으로 미룬다
    /// </summary>
    public class SettingsOpenedMessage
    {
    }

    /// <summary>
    /// 타이틀 바 업데이트 아이콘을 강제로 켜는 메시지 (SettingsViewModel → MainWindowViewModel)
    /// 개발 빌드에서는 Velopack 업데이트가 잡히지 않아 아이콘이 뜰 일이 없어, 모양을 확인할 길을 둔다
    /// </summary>
    public class UpdateIconPreviewMessage : ValueChangedMessage<bool>
    {
        public UpdateIconPreviewMessage(bool value) : base(value) { }
    }
}
