using SpaceMousePilot.Enums;

namespace SpaceMousePilot.ViewModels;

public sealed class ButtonMappingViewModel : ObservableObject
{
    private readonly Dictionary<string, GamepadButton> _source;
    private readonly string _key;

    public string Label { get; }
    public static GamepadButton[] Options => Enum.GetValues<GamepadButton>();

    public GamepadButton Selected
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            _source[_key] = value;
            Notify();
        }
    }

    public ButtonMappingViewModel(string key, GamepadButton initial, Dictionary<string, GamepadButton> source)
    {
        _key = key;
        _source = source;
        Label = $"Button {key}";
        Selected = initial;
    }
}
