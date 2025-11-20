using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TanukiTarkovMap.Converters
{
    /// <summary>
    /// bool 값을 색상으로 변환하는 컨버터
    /// true = Red (핀 활성화), false = White (핀 비활성화)
    /// </summary>
    public class BoolToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive && isActive)
            {
                return Brushes.Red;
            }
            return Brushes.White;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
