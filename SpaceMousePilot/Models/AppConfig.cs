using System.Text.Json.Serialization;

namespace SpaceMousePilot.Models;

public sealed class AppConfig
{
    [JsonPropertyName("poll_rate_hz")]     public int    PollRateHz     { get; set; } = 250;
    [JsonPropertyName("ui_refresh_hz")]    public int    UiRefreshHz    { get; set; } = 30;
    [JsonPropertyName("calib_duration_s")] public double CalibDurationS { get; set; } = 10.0;

    [JsonPropertyName("axes")]
    public Dictionary<string, AxisConfig> Axes { get; set; } = new()
    {
        ["roll"]  = new() { Gamepad = "right_x", Sensitivity = 1.0, Deadzone = 0.05, Invert = false, Curve = 1.8, Scale = 350 },
        ["pitch"] = new() { Gamepad = "right_y", Sensitivity = 1.0, Deadzone = 0.05, Invert = true,  Curve = 1.8, Scale = 350 },
        ["yaw"]   = new() { Gamepad = "left_x",  Sensitivity = 0.8, Deadzone = 0.10, Invert = false, Curve = 2.5, Scale = 350 },
        ["z"]     = new() { Gamepad = "left_y",  Sensitivity = 0.8, Deadzone = 0.08, Invert = false, Curve = 1.5, Scale = 350 },
    };

    [JsonPropertyName("buttons")]
    public Dictionary<string, string> Buttons { get; set; } = new()
    {
        ["0"] = "A",
        ["1"] = "B",
    };
}
