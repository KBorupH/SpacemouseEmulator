using System.Globalization;
using System.Windows.Data;

using SpaceMousePilot.Enums;

namespace SpaceMousePilot.Converters;

/// <summary>Converts GamepadAxis and GamepadButton enums to friendly display names.</summary>
public sealed class EnumDisplayConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c) => value switch
    {
        GamepadAxis axis => axis.ToDisplayName(),
        GamepadButton btn => btn.ToString(),
        _ => value?.ToString() ?? "",
    };

    public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
        => throw new NotSupportedException();
}
