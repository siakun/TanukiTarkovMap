using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TanukiTarkovMap.Converters
{
    /// <summary>
    /// bool 값을 토글 버튼 배경색으로 변환하는 컨버터
    /// parameter가 "Invert"인 경우 반전된 값으로 판단
    /// true = 활성화 색상 (#FF4A90D9), false = 투명
    /// </summary>
    public class BoolToToggleBackgroundConverter : IValueConverter
    {
        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9));
        private static readonly Brush InactiveBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
                // Invert 파라미터가 있으면 값을 반전
                if (parameter is string param && param == "Invert")
                {
                    isActive = !isActive;
                }

                return isActive ? ActiveBrush : InactiveBrush;
            }
            return InactiveBrush;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
