using CommunityToolkit.Mvvm.ComponentModel;
using SpaceMousePilot.Models;

namespace SpaceMousePilot.ViewModels;

public sealed partial class AxisViewModel : ObservableObject
{
    private readonly AxisConfig _cfg;

    public string Key   { get; }
    public string Label { get; }
    public string Hint  { get; }

    public static string[] GamepadAxes   => ["left_x", "left_y", "right_x", "right_y", "lt", "rt", "sl0", "sl1"];

    public AxisViewModel(string key, AxisConfig cfg)
    {
        Key   = key;
        _cfg  = cfg;
        Label = key switch { "roll" => "Roll", "pitch" => "Pitch", "yaw" => "Yaw", _ => "Collective" };
        Hint  = key switch
        {
            "roll"  => "Tilt device left / right",
            "pitch" => "Tilt device forward / back",
            "yaw"   => "Twist device",
            _       => "Push device up / down",
        };

        // Initialise backing fields from model
        _sensitivity = cfg.Sensitivity;
        _deadzone    = cfg.Deadzone;
        _curve       = cfg.Curve;
        _scale       = cfg.Scale;
        _invert      = cfg.Invert;
        _gamepad     = cfg.Gamepad;
    }

    [ObservableProperty] private double _sensitivity;
    [ObservableProperty] private double _deadzone;
    [ObservableProperty] private double _curve;
    [ObservableProperty] private int    _scale;
    [ObservableProperty] private bool   _invert;
    [ObservableProperty] private string _gamepad;

    partial void OnSensitivityChanged(double value) => _cfg.Sensitivity = value;
    partial void OnDeadzoneChanged(double value)    => _cfg.Deadzone    = value;
    partial void OnCurveChanged(double value)       => _cfg.Curve       = value;
    partial void OnScaleChanged(int value)          => _cfg.Scale       = value;
    partial void OnInvertChanged(bool value)        => _cfg.Invert      = value;
    partial void OnGamepadChanged(string value)     => _cfg.Gamepad     = value;

    /// <summary>Called after calibration to push measured scale back to UI.</summary>
    public void RefreshScale() => Scale = _cfg.Scale;
}
