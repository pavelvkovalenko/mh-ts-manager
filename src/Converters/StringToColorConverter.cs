using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace MhTsManager.Converters
{
    /// <summary>
    /// Конвертер текста статуса в цвет индикатора.
    /// </summary>
    public class StringToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value switch
            {
                "Активна" or "Active" => new SolidColorBrush(Color.FromRgb(16, 185, 129)),     // 🟢
                "Бездействие" or "Idle" => new SolidColorBrush(Color.FromRgb(245, 158, 11)),   // 🟡
                "Отключена" or "Disconnected" => new SolidColorBrush(Color.FromRgb(107, 114, 128)), // ⚪
                "Заблокирована" or "Locked" => new SolidColorBrush(Color.FromRgb(99, 102, 241)),   // 🔵
                "Недоступна" or "Unavailable" => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // 🔴
                _ => new SolidColorBrush(Color.FromRgb(156, 163, 175))
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
