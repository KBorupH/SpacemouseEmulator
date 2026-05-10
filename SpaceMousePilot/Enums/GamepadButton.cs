using System.Text.Json.Serialization;

namespace SpaceMousePilot.Enums;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum GamepadButton { A, B, X, Y, LB, RB, LS, RS }
