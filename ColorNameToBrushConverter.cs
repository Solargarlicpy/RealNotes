using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace RealNotes
{
 public class ColorNameToBrushConverter : IValueConverter
 {
 public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
 {
 if (value is string name)
 {
 return name switch
 {
 "Yellow" => Brushes.Yellow,
 "Green" => Brushes.LightGreen,
 "Blue" => Brushes.LightBlue,
 "Pink" => Brushes.LightPink,
 "White" => Brushes.White,
 "Chartreuse" => Brushes.Chartreuse,
 "Violet" => Brushes.Violet,
 "Orange" => Brushes.Orange,
 _ => Brushes.LightGray
 };
 }

 return Brushes.LightGray;
 }

 public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
 {
 throw new NotImplementedException();
 }
 }
}
