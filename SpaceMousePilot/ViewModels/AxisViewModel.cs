using SpaceMousePilot.Enums;
using SpaceMousePilot.Models;

namespace SpaceMousePilot.ViewModels;

public sealed class AxisViewModel(AxisKey axis, AxisConfig cfg) : ObservableObject
{
    private readonly AxisConfig _cfg = cfg;

    public AxisKey AxisKey { get; } = axis;
    public string Label { get; } = axis.ToLabel();
    public string Hint { get; } = axis.ToHint();

    public static GamepadAxis[] GamepadAxes => Enum.GetValues<GamepadAxis>();

    public double Sensitivity
    {
        get => _cfg.Sensitivity;
        set
        {
            if (_cfg.Sensitivity == value)
                return;

            _cfg.Sensitivity = value;
            Notify();
        }
    }

    public double Deadzone
    {
        get => _cfg.Deadzone;
        set
        {
            if (_cfg.Deadzone == value)
                return;

            _cfg.Deadzone = value;
            Notify();
        }
    }

    public double Curve
    {
        get => _cfg.Curve;
        set
        {
            if (_cfg.Curve == value)
                return;

            _cfg.Curve = value;
            Notify();
        }
    }

    public int Scale
    {
        get => _cfg.Scale;
        set
        {
            if (_cfg.Scale == value)
                return;

            _cfg.Scale = value;
            Notify();
        }
    }

    public bool Invert
    {
        get => _cfg.Invert;
        set
        {
            if (_cfg.Invert == value)
                return;

            _cfg.Invert = value;
            Notify();
        }
    }

    public GamepadAxis Gamepad
    {
        get => _cfg.Gamepad;
        set
        {
            if (_cfg.Gamepad == value)
                return;

            _cfg.Gamepad = value;
            Notify();
        }
    }

    /// <summary>Called after calibration to push measured scale back to UI.</summary>
    public void RefreshScale() => Notify(nameof(Scale));
}
