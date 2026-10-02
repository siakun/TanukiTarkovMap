using System.Windows;
using System.Windows.Controls.Primitives;

/**
CalloutToolTipPlacement - 꼬리 달린 툴팁을 대상 바로 아래 가운데에 놓는 배치 콜백

Purpose: CalloutToolTip 스타일은 툴팁 위쪽 가운데에 대상을 가리키는 꼬리를 그린다. 꼬리가 버튼을
가리키려면 툴팁의 가로 중앙이 버튼의 가로 중앙과 맞아야 하는데, Placement="Bottom"은 툴팁 왼쪽 끝을
버튼 왼쪽 끝에 맞춘다. 툴팁 폭은 글자에 따라 달라지므로 고정 HorizontalOffset으로는 맞출 수 없다.

Architecture: CalloutToolTip 스타일이 Placement="Custom"과 함께 CustomPopupPlacementCallback에
x:Static으로 BelowCenter를 지정한다. 스타일 하나만 적용하면 모양과 위치가 함께 따라온다.

Key Methods:
- BelowCenter(popupSize, targetSize, offset): 툴팁 가로 중앙을 대상 가로 중앙에, 툴팁 위 끝을 대상
  아래 끝에 맞춘 위치 하나를 돌려준다. offset 인자는 쓰지 않는다 (Critical Warnings 참고)

Design Rationale: Behavior가 아니라 정적 콜백으로 둔 것은 Behavior를 Style에서 붙일 수 없기 때문이다.
Behavior로 만들면 이 툴팁을 쓰는 자리마다 따로 붙여야 하고, 빠뜨리면 꼬리가 버튼이 아닌 곳을 가리킨다.
버튼과 꼬리 사이의 틈은 여기서 더하지 않고 템플릿 여백으로 둔다 (아래 단위 문제 때문이다).

Critical Warnings: 콜백 인자의 단위가 섞여 있다. WPF Popup.UpdatePosition은 popupSize와 targetSize를
장치 픽셀로 넘기고 돌려받은 위치도 장치 픽셀로 해석하지만, offset은 HorizontalOffset/VerticalOffset을
DIP 그대로 담아 넘긴다. 상수나 offset을 위치에 더하면 DPI 배율이 100%가 아닐 때 그만큼 어긋난다.
두 크기의 차이만 쓰면 단위와 상관없이 가운데가 맞는다.

Known Limitations: 툴팁이 화면 가장자리에 걸리면 WPF가 화면 안으로 밀어 넣으므로 꼬리가 대상
중앙에서 벗어날 수 있다. 지금 쓰는 업데이트 버튼은 오른쪽에 설정, 핀, 닫기 버튼이 있어 창 오른쪽
끝에서 툴팁 반 폭보다 안쪽에 있으므로, 창을 화면 오른쪽 끝에 붙여도 걸리지 않는다. 창 가장자리에
붙은 버튼에 이 툴팁을 쓰려면 툴팁이 밀려난 만큼 꼬리를 반대로 옮기는 처리가 필요하다.

Last Updated: 2026-10-02 | .NET 8 | 업데이트 준비 툴팁을 말풍선 모양으로 바꾸며 추가
*/
namespace TanukiTarkovMap.Behaviors
{
    public static class CalloutToolTipPlacement
    {
        public static readonly CustomPopupPlacementCallback BelowCenter = (popupSize, targetSize, offset) =>
            new[]
            {
                new CustomPopupPlacement(
                    new Point((targetSize.Width - popupSize.Width) / 2, targetSize.Height),
                    PopupPrimaryAxis.Horizontal)
            };
    }
}
