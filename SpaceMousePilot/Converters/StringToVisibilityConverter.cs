using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace SpaceMousePilot.Converters;

/// <summary>Collapses when string is null or empty, visible otherwise.</summary>
public sealed class StringToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
        => string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
        => throw new NotSupportedException();
}
