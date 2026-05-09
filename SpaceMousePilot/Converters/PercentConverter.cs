using System.Globalization;
using System.Windows.Data;

namespace SpaceMousePilot.Converters;

/// <summary>Converts 0.0–1.0 to 0–100 for ProgressBar.Value.</summary>
public sealed class PercentConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is double d ? d * 100 : 0;

    public object ConvertBack(object value, Type t, object p, CultureInfo c)
        => value is double d ? d / 100 : 0;
}
