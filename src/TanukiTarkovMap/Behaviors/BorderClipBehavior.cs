using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Xaml.Behaviors;

/**
BorderClipBehavior - Border의 둥근 모서리에 맞춰 자식을 잘라내는 Behavior

Purpose: WPF의 Border.CornerRadius는 자기 배경만 둥글게 할 뿐 자식은 그대로 두어,
모서리를 채우는 자식(창과 패널의 닫기 버튼)이 곡선 밖으로 삐져나온다. Clip을 걸어 막는다.

Method Flow:
  Loaded / SizeChanged -> UpdateClip -> Border.Clip에 RectangleGeometry 설정

Critical Warnings: 테두리가 없는 Border에 붙여야 한다.
BorderThickness가 있으면 자식은 그 두께만큼 안쪽에서 시작하는데 이 Behavior는 Border 전체를
바깥 반경으로 자른다. 그래서 자식 모서리가 덜 깎여 각진 채로 남는다. 반경 6에 테두리 1인
설정 패널에 직접 붙였을 때 닫기 버튼 모서리가 1.07px만 깎여 눈에 띄게 각져 보였고, 안쪽에
전용 Border를 두니 2.07px가 깎여 의도한 곡선이 나왔다 (2026-08). 잘리기는 하므로 로그도
예외도 남지 않아, 확대해서 재보기 전에는 알아차리기 어렵다.

올바른 짜임 (메인 창과 설정 패널이 모두 이 형태다):
  Border   테두리와 반경을 그린다      CornerRadius=8, BorderThickness=1
    +- Border   자르기만 한다          CornerRadius=7, 테두리 없음, 이 Behavior
         +- 실제 내용

Known Limitations: RectangleGeometry는 모서리마다 다른 반경을 줄 수 없어 TopLeft 값을 네 곳에
함께 쓴다. 위쪽만 둥근 Border(CornerRadius="6,6,0,0")에 붙이면 아래 모서리까지 깎여 아래
내용과 사이가 벌어지므로, 그런 자리에는 붙이지 않는다.

Last Updated: 2026-08-15 | .NET 8 | 설정 패널에 적용하며 겪은 주의사항 반영
*/
namespace TanukiTarkovMap.Behaviors
{
    public class BorderClipBehavior : Behavior<Border>
    {
        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.SizeChanged += OnSizeChanged;
            AssociatedObject.Loaded += OnLoaded;
        }

        protected override void OnDetaching()
        {
            AssociatedObject.SizeChanged -= OnSizeChanged;
            AssociatedObject.Loaded -= OnLoaded;
            base.OnDetaching();
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClip();
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateClip();
        }

        private void UpdateClip()
        {
            var border = AssociatedObject;
            if (border.ActualWidth <= 0 || border.ActualHeight <= 0)
                return;

            var cornerRadius = border.CornerRadius;

            // 모서리마다 다른 반경을 줄 수 없어 TopLeft 값을 네 곳에 함께 쓴다.
            // 자르는 범위가 Border 전체이므로, 붙인 Border에 테두리가 있으면
            // 안쪽에서 시작하는 자식이 그 두께만큼 덜 깎인다 (클래스 주석 참고)
            var clip = new RectangleGeometry
            {
                Rect = new Rect(0, 0, border.ActualWidth, border.ActualHeight),
                RadiusX = cornerRadius.TopLeft,
                RadiusY = cornerRadius.TopLeft
            };

            border.Clip = clip;
        }
    }
}
