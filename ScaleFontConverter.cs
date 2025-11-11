using System.Windows.Markup;
using System.Globalization;
using System.Windows.Data;


namespace RealNotes   // This should match your project's namespace
{
    public class ScaleFontConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is double width &&
                parameter != null &&
                double.TryParse(parameter.ToString(), out double scale))
            {
                double fontSize = width * scale;
                // keep it between 12 and 18 pixels
                return Math.Min(Math.Max(fontSize, 12), 18);
            }
            return 14;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotImplementedException();
    }
}
