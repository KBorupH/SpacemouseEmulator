using System.Text.Json.Serialization;

namespace SpaceMousePilot.Models;

public sealed class AxisConfig
{
    [JsonPropertyName("gamepad")]     public string Gamepad     { get; set; } = "right_x";
    [JsonPropertyName("sensitivity")] public double Sensitivity { get; set; } = 1.0;
    [JsonPropertyName("deadzone")]    public double Deadzone    { get; set; } = 0.05;
    [JsonPropertyName("invert")]      public bool   Invert      { get; set; } = false;
    [JsonPropertyName("curve")]       public double Curve       { get; set; } = 1.8;
    [JsonPropertyName("scale")]       public int    Scale       { get; set; } = 350;
}
