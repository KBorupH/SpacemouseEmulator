using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SpaceMousePilot.Models;
using SpaceMousePilot.Services;

namespace SpaceMousePilot.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    public static string[] GamepadAxes => AxisViewModel.GamepadAxes;

    // ── state ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private bool   _isRunning;
    [ObservableProperty] private bool   _spacemouseOk;
    [ObservableProperty] private bool   _gamepadOk;
    [ObservableProperty] private string _deviceName      = "";
    [ObservableProperty] private string _errorText       = "";
    [ObservableProperty] private bool   _showVigemLink;
    [ObservableProperty] private bool   _isCalibrating;
    [ObservableProperty] private double _calibProgress;
    [ObservableProperty] private string _calibHint       = "Move device through full range when calibrating";
    [ObservableProperty] private bool   _calibHintUrgent;
    [ObservableProperty] private string _saveLabel       = "";
    [ObservableProperty] private bool   _bridgeStarting;

    // ── data ──────────────────────────────────────────────────────────────────
    public ObservableCollection<MeterViewModel> Meters { get; } = [];
    public ObservableCollection<AxisViewModel> AxisTabs { get; } = [];
    public ObservableCollection<AxisViewModel> MappingAxes { get; } = [];
    public ObservableCollection<ButtonMappingViewModel> Buttons { get; } = [];

    public AppConfig Config { get; }

    // ── perf sliders ──────────────────────────────────────────────────────────
    [ObservableProperty] private int    _pollRateHz;
    [ObservableProperty] private int    _uiRefreshHz;
    [ObservableProperty] private double _calibDurationS;

    partial void OnPollRateHzChanged(int value)      
        => Config.PollRateHz = value;

    partial void OnUiRefreshHzChanged(int value)
    { 
        Config.UiRefreshHz = value; 
        _uiTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, value)); 
    }

    partial void OnCalibDurationSChanged(double value) 
        => Config.CalibDurationS = value;

    // ── internals ─────────────────────────────────────────────────────────────
    private readonly BridgeService  _bridge;
    private readonly DispatcherTimer _uiTimer;
    private readonly Dispatcher      _dispatcher;
    private DispatcherTimer?         _saveLabelTimer;

    public MainViewModel(AppConfig config, Dispatcher dispatcher)
    {
        Config      = config;
        _dispatcher = dispatcher;

        _pollRateHz     = config.PollRateHz;
        _uiRefreshHz    = config.UiRefreshHz;
        _calibDurationS = config.CalibDurationS;

        BuildCollections();

        _bridge = new BridgeService(config);
        SubscribeBridgeEvents();

        _uiTimer = new DispatcherTimer(
            TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, config.UiRefreshHz)),
            DispatcherPriority.Background,
            (_, _) => RefreshMeters(),
            dispatcher);
        _uiTimer.Start();
    }

    // ── collections ───────────────────────────────────────────────────────────

    private void BuildCollections()
    {
        string[] axisKeys = ["roll", "pitch", "yaw", "z"];
        string[] labels   = ["Roll", "Pitch", "Yaw", "Collective"];

        for (int i = 0; i < axisKeys.Length; i++)
        {
            Meters.Add(new MeterViewModel(labels[i], axisKeys[i]));
            AxisTabs.Add(new AxisViewModel(axisKeys[i], Config.Axes[axisKeys[i]]));
            MappingAxes.Add(new AxisViewModel(axisKeys[i], Config.Axes[axisKeys[i]]));
        }

        foreach (var (key, val) in Config.Buttons.OrderBy(kv => kv.Key))
            Buttons.Add(new ButtonMappingViewModel(key, val, Config.Buttons));
    }

    // ── bridge events ─────────────────────────────────────────────────────────

    private void SubscribeBridgeEvents()
    {
        _bridge.OnError          += msg  => Dispatch(() => { ErrorText = msg; ShowVigemLink = msg.Contains("ViGEmBus"); BridgeStarting = false; });
        _bridge.OnGamepadReady   += ok   => Dispatch(() => GamepadOk = ok);
        _bridge.OnDeviceConnected += name => Dispatch(() => { DeviceName = name; SpacemouseOk = true; });
        _bridge.OnRunning        += ()   => Dispatch(() => { IsRunning = true; ErrorText = ""; BridgeStarting = false; });
        _bridge.OnStopped        += ()   => Dispatch(() => { IsRunning = false; SpacemouseOk = false; GamepadOk = false; DeviceName = ""; });
        _bridge.OnAxisValue      += (axis, val) => UpdateAxisValue(axis, val);
        _bridge.OnCalibProgress  += p    => Dispatch(() =>
        {
            CalibProgress = p;
            var remaining = (int)(Config.CalibDurationS * (1 - p)) + 1;
            CalibHint = $"Move ALL axes to their limits! ({remaining}s)";
        });
        _bridge.OnCalibPeak      += (axis, frac) => Dispatch(() => GetMeter(axis)!.CalibPeak = frac);
        _bridge.OnCalibComplete  += _ => Dispatch(() =>
        {
            IsCalibrating = false;
            CalibProgress = 0;
            CalibHint     = "Move device through full range when calibrating";
            foreach (var ax in AxisTabs) ax.RefreshScale();
            foreach (var ax in MappingAxes) ax.RefreshScale();
            SetCalibMode(false);
            ConfigService.Save(Config);
            ShowSave("Calibrated ✓");
            CalibrateCommand.NotifyCanExecuteChanged();
        });
    }

    private void UpdateAxisValue(string axis, double val)
    {
        var m = GetMeter(axis);
        if (m is not null) Dispatch(() => m.Value = val);
    }

    private MeterViewModel? GetMeter(string axis) => axis switch
    {
        "roll"  => Meters[0], "pitch" => Meters[1],
        "yaw"   => Meters[2], "z"     => Meters[3],
        _ => null,
    };

    private void SetCalibMode(bool on)
    {
        foreach (var m in Meters) m.IsCalibMode = on;
    }

    // ── commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task ToggleBridge()
    {
        if (IsRunning)
        {
            await Task.Run(_bridge.Stop);
            return;
        }

        ErrorText      = "";
        ShowVigemLink  = false;
        BridgeStarting = true;

        bool ok = await Task.Run(_bridge.Start);
        if (!ok) BridgeStarting = false;
    }

    [RelayCommand(CanExecute = nameof(CanCalibrate))]
    private void Calibrate()
    {
        IsCalibrating = true;
        SetCalibMode(true);
        foreach (var m in Meters) m.CalibPeak = 0;
        _bridge.StartCalibration();
        CalibrateCommand.NotifyCanExecuteChanged();
    }

    private bool CanCalibrate() => IsRunning && !IsCalibrating;

    [RelayCommand]
    private void Save()
    {
        ConfigService.Save(Config);
        ShowSave("Saved ✓");
    }

    [RelayCommand]
    private void OpenLog()
    {
        try
        {
            if (!File.Exists(Logger.FilePath)) File.Create(Logger.FilePath).Dispose();
            Process.Start(new ProcessStartInfo("notepad.exe", Logger.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex) { ErrorText = $"Could not open log: {ex.Message}"; }
    }

    [RelayCommand]
    private void OpenConfigFolder()
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", ConfigService.FolderPath) { UseShellExecute = true }); }
        catch (Exception ex) { ErrorText = $"Could not open folder: {ex.Message}"; }
    }

    [RelayCommand]
    private static void OpenVigemLink()
        => Process.Start(new ProcessStartInfo("https://github.com/nefarius/ViGEmBus/releases/latest") { UseShellExecute = true });

    // ── UI refresh ────────────────────────────────────────────────────────────

    private void RefreshMeters()
    {
        foreach (var m in Meters)
            m.NotifyIsActive();
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private void Dispatch(Action a) => _dispatcher.Invoke(a);

    private void ShowSave(string text)
    {
        SaveLabel = text;
        _saveLabelTimer?.Stop();
        _saveLabelTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _saveLabelTimer.Tick += (_, _) => { SaveLabel = ""; _saveLabelTimer.Stop(); };
        _saveLabelTimer.Start();
    }

    public void Shutdown()
    {
        _uiTimer.Stop();
        _bridge.Stop();
        ConfigService.Save(Config);
    }

    partial void OnIsRunningChanged(bool value)
        => CalibrateCommand.NotifyCanExecuteChanged();
}
