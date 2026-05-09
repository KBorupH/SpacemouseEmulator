using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SpaceMousePilot.Converters;

/// <summary>Converts bool → SolidColorBrush. Parameter = "true_color|false_color" in hex.</summary>
public sealed class BoolToColorConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        var parts = (parameter as string ?? "#3FB95A|#F05050").Split('|');
        var hex   = value is true ? parts[0] : parts[1];
        return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
    }

    public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
        => throw new NotSupportedException();
}
