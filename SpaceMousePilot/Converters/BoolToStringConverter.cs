using System.Globalization;
using System.Windows.Data;

namespace SpaceMousePilot.Converters;

/// <summary>Returns a string from two pipe-delimited options based on bool value.</summary>
public sealed class BoolToStringConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        var parts = (parameter as string ?? "Yes|No").Split('|');
        return value is true ? parts[0] : parts[1];
    }

    public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
        => throw new NotSupportedException();
}
