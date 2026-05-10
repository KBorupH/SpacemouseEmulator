using System.Text.Json.Serialization;

namespace SpaceMousePilot.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AxisKey
{
    Roll,
    Pitch,
    Yaw,
    Collective,
}

public static class AxisKeyExtensions
{
    public static string ToConfigKey(this AxisKey axis) => axis switch
    {
        AxisKey.Roll => "roll",
        AxisKey.Pitch => "pitch",
        AxisKey.Yaw => "yaw",
        AxisKey.Collective => "z",
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };

    public static string ToLabel(this AxisKey axis) => axis switch
    {
        AxisKey.Roll => "Roll",
        AxisKey.Pitch => "Pitch",
        AxisKey.Yaw => "Yaw",
        AxisKey.Collective => "Collective",
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };

    public static string ToHint(this AxisKey axis) => axis switch
    {
        AxisKey.Roll => "Tilt device left / right",
        AxisKey.Pitch => "Tilt device forward / back",
        AxisKey.Yaw => "Twist device",
        AxisKey.Collective => "Push device up / down",
        _ => throw new ArgumentOutOfRangeException(nameof(axis)),
    };
}
