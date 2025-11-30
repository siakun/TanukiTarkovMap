using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Xaml.Behaviors;
using TanukiTarkovMap.Messages;

namespace TanukiTarkovMap.Behaviors
{
    /**
    OpacitySliderDragBehavior - 슬라이더 드래그 상태 감지 및 메시지 전송

    Purpose: 투명도 슬라이더의 Thumb 드래그 시작/종료를 감지하여 ViewModel에 알림
    Architecture: Behavior<Slider> → WeakReferenceMessenger → MainWindowViewModel

    Core Functionality:
    - DragStarted: 드래그 시작 시 OpacitySliderDragMessage(true) 전송
    - DragCompleted: 드래그 종료 시 OpacitySliderDragMessage(false) 전송

    Design Rationale:
    - Slider의 Thumb은 로드 후에만 접근 가능하므로 Loaded 이벤트에서 찾음
    - VisualTreeHelper 대신 Template.FindName 사용 (더 안정적)

    Last Updated: 2025-11-30
    */
    public class OpacitySliderDragBehavior : Behavior<Slider>
    {
        private Thumb? _thumb;

        protected override void OnAttached()
        {
            base.OnAttached();
            AssociatedObject.Loaded += OnSliderLoaded;
        }

        protected override void OnDetaching()
        {
            if (_thumb != null)
            {
                _thumb.DragStarted -= OnDragStarted;
                _thumb.DragCompleted -= OnDragCompleted;
            }

            AssociatedObject.Loaded -= OnSliderLoaded;
            base.OnDetaching();
        }

        private void OnSliderLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Slider 템플릿에서 Thumb 찾기
            AssociatedObject.ApplyTemplate();
            var track = AssociatedObject.Template.FindName("PART_Track", AssociatedObject) as Track;
            _thumb = track?.Thumb;

            if (_thumb != null)
            {
                _thumb.DragStarted += OnDragStarted;
                _thumb.DragCompleted += OnDragCompleted;
            }
        }

        private void OnDragStarted(object sender, DragStartedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new OpacitySliderDragMessage(true));
        }

        private void OnDragCompleted(object sender, DragCompletedEventArgs e)
        {
            WeakReferenceMessenger.Default.Send(new OpacitySliderDragMessage(false));
        }
    }
}
