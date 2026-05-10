using System.Text.Json.Serialization;

using SpaceMousePilot.Enums;

namespace SpaceMousePilot.Models;

public sealed class AppConfig
{
    [JsonPropertyName("poll_rate_hz")] public int PollRateHz { get; set; } = 250;
    [JsonPropertyName("ui_refresh_hz")] public int UiRefreshHz { get; set; } = 30;
    [JsonPropertyName("calib_duration_s")] public double CalibDurationS { get; set; } = 10.0;

    [JsonPropertyName("axes")]
    public Dictionary<AxisKey, AxisConfig> Axes { get; set; } = new()
    {
        [AxisKey.Roll] = new() { Gamepad = GamepadAxis.right_x, Sensitivity = 1.0, Deadzone = 0.05, Invert = false, Curve = 1.8, Scale = 350 },
        [AxisKey.Pitch] = new() { Gamepad = GamepadAxis.right_y, Sensitivity = 1.0, Deadzone = 0.05, Invert = true, Curve = 1.8, Scale = 350 },
        [AxisKey.Yaw] = new() { Gamepad = GamepadAxis.left_x, Sensitivity = 0.8, Deadzone = 0.10, Invert = false, Curve = 2.5, Scale = 350 },
        [AxisKey.Collective] = new() { Gamepad = GamepadAxis.left_y, Sensitivity = 0.8, Deadzone = 0.08, Invert = false, Curve = 1.5, Scale = 350 },
    };

    [JsonPropertyName("buttons")]
    public Dictionary<string, GamepadButton> Buttons { get; set; } = new()
    {
        ["0"] = GamepadButton.A,
        ["1"] = GamepadButton.B,
    };
}
