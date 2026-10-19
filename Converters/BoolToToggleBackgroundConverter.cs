using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TanukiTarkovMap.Converters
{
    public class BoolToToggleBackgroundConverter : IValueConverter
    {
        private static readonly Brush ActiveBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x90, 0xD9));
        private static readonly Brush InactiveBrush = Brushes.Transparent;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool isActive)
            {
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
