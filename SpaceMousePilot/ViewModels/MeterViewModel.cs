using SpaceMousePilot.Enums;

namespace SpaceMousePilot.ViewModels;

public sealed class MeterViewModel(string label, AxisKey axisKey) : ObservableObject
{
    public string Label { get; } = label;
    public AxisKey AxisKey { get; } = axisKey;

    public double Value
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            Notify(nameof(ValueText));
            Notify(nameof(IsActive));
        }
    }

    public double CalibPeak
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            Notify(nameof(ValueText));
            Notify(nameof(IsActive));
        }
    }

    public bool IsCalibMode
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            Notify(nameof(ValueText));
            Notify(nameof(IsActive));
        }
    }

    public string ValueText => IsCalibMode
        ? $"{(int)(CalibPeak * 100)}%"
        : $"{(Value >= 0 ? "+" : "")}{Value:F2}";

    public bool IsActive => IsCalibMode ? CalibPeak > 0.1 : Math.Abs(Value) > 0.02;

    public void NotifyIsActive() => Notify(nameof(IsActive));
}
