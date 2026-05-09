using CommunityToolkit.Mvvm.ComponentModel;

namespace SpaceMousePilot.ViewModels;

public sealed partial class MeterViewModel(string label, string axisKey) : ObservableObject
{
    public string Label { get; } = label;
    public string AxisKey { get; } = axisKey;

    [ObservableProperty] private double _value;
    [ObservableProperty] private double _calibPeak;
    [ObservableProperty] private bool   _isCalibMode;

    public string ValueText => IsCalibMode
        ? $"{(int)(CalibPeak * 100)}%"
        : $"{(Value >= 0 ? "+" : "")}{Value:F2}";

    /// <summary>True when the meter has meaningful deflection to highlight.</summary>
    public bool IsActive => IsCalibMode 
        ? CalibPeak > 0.1 
        : Math.Abs(Value) > 0.02;

    /// <summary>Called by the UI timer to nudge IsActive without a full value change.</summary>
    public void NotifyIsActive() => OnPropertyChanged(nameof(IsActive));

    partial void OnValueChanged(double value)
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(IsActive));
    }

    partial void OnCalibPeakChanged(double value)
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(IsActive));
    }

    partial void OnIsCalibModeChanged(bool value)
    {
        OnPropertyChanged(nameof(ValueText));
        OnPropertyChanged(nameof(IsActive));
    }
}
