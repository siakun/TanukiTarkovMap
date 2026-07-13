using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 물리적 이동이 없는 중복 MouseMove를 차단하는 Behavior (CefSharp 브라우저용)
    ///
    /// 문제: WPF는 마우스 버튼을 누르는 순간(마우스 캡처 시작) 커서가 움직이지 않아도
    /// 같은 좌표의 MouseMove를 한 번 더 발생시킨다. CefSharp가 이를 페이지에 전달하면
    /// tarkov-market 맵의 팬 로직이 "버튼 누른 채 move = 드래그"로 판정해
    /// 마커 좌클릭이 클릭으로 인정되지 않는다 (마커 팝업이 열리지 않는 원인).
    ///
    /// 해결: 버튼이 눌린 상태에서 직전과 같은 좌표로 오는 MouseMove를 Preview 단계에서
    /// 차단한다. Preview와 버블링 이벤트는 EventArgs를 공유하므로 Handled = true로
    /// ChromiumWebBrowser의 OnMouseMove까지 도달하지 않는다.
    /// 좌표가 실제로 변한 move는 통과하므로 맵 드래그 팬 동작에는 영향이 없다.
    /// </summary>
    public class DuplicateMouseMoveFilterBehavior : Behavior<UIElement>
    {
        private Point _lastMovePosition = new Point(double.NaN, double.NaN);

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.PreviewMouseDown += OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove += OnPreviewMouseMove;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.PreviewMouseDown -= OnPreviewMouseDown;
            AssociatedObject.PreviewMouseMove -= OnPreviewMouseMove;
            base.OnDetaching();
        }

        private void OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // 누른 좌표를 비교 기준으로 설정 (직후 발생하는 유령 move와 대조)
            _lastMovePosition = e.GetPosition(AssociatedObject);
        }

        private void OnPreviewMouseMove(object sender, MouseEventArgs e)
        {
            var movePosition = e.GetPosition(AssociatedObject);

            bool buttonPressed = e.LeftButton == MouseButtonState.Pressed
                || e.RightButton == MouseButtonState.Pressed
                || e.MiddleButton == MouseButtonState.Pressed;

            // 버튼이 눌린 상태의 이동량 0 move만 차단한다.
            // 버튼이 없는 중복 move는 호버 갱신에 쓰일 수 있으므로 통과시킨다
            if (buttonPressed && movePosition == _lastMovePosition)
            {
                e.Handled = true;
                return;
            }

            _lastMovePosition = movePosition;
        }
    }
}
