using CommunityToolkit.Mvvm.ComponentModel;

namespace SpaceMousePilot.ViewModels;

public sealed partial class ButtonMappingViewModel : ObservableObject
{
    private readonly Dictionary<string, string> _source;
    private readonly string _key;

    public string Label { get; }
    public static string[] Options => ["A", "B", "X", "Y", "LB", "RB", "LS", "RS"];

    [ObservableProperty] private string _selected;

    public ButtonMappingViewModel(string key, string initial, Dictionary<string, string> source)
    {
        _key      = key;
        _source   = source;
        Label     = $"Button {key}";
        _selected = initial;
    }

    partial void OnSelectedChanged(string value) => _source[_key] = value;
}
