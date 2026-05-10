using System.Text.Json.Serialization;

namespace SpaceMousePilot.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamepadAxis
{
    left_x,
    left_y,
    right_x,
    right_y,
    lt,
    rt,
    sl0,
    sl1,
}

public static class GamepadAxisExtensions
{
    public static string ToDisplayName(this GamepadAxis axis) => axis switch
    {
        GamepadAxis.left_x => "Left X-Axis",
        GamepadAxis.left_y => "Left Y-Axis",
        GamepadAxis.right_x => "Right X-Axis",
        GamepadAxis.right_y => "Right Y-Axis",
        GamepadAxis.lt => "LT",
        GamepadAxis.rt => "RT",
        GamepadAxis.sl0 => "Slider 0",
        GamepadAxis.sl1 => "Slider 1",
        _ => axis.ToString(),
    };
}
