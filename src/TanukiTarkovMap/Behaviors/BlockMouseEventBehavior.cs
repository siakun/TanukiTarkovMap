using System.Windows;
using System.Windows.Input;
using Microsoft.Xaml.Behaviors;

namespace TanukiTarkovMap.Behaviors
{
    /// <summary>
    /// 마우스 클릭 이벤트의 버블링을 차단하는 Behavior
    /// 오버레이 패널에서 내부 요소 클릭 시 배경 클릭 이벤트가 발생하지 않도록 함
    /// MouseLeftButtonDown (버블링)에서 처리하여 자식 요소의 클릭은 정상 동작
    /// </summary>
    public class BlockMouseEventBehavior : Behavior<UIElement>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.MouseLeftButtonDown += OnMouseLeftButtonDown;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.MouseLeftButtonDown -= OnMouseLeftButtonDown;
            base.OnDetaching();
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 버블링 이벤트 차단 (부모로 전파되지 않음)
            e.Handled = true;
        }
    }
}
