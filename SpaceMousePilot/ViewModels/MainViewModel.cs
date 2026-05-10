using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using System.Windows.Threading;

using SpaceMousePilot.Enums;
using SpaceMousePilot.Models;
using SpaceMousePilot.Services;

namespace SpaceMousePilot.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    // ── state ─────────────────────────────────────────────────────────────────

    public bool IsRunning
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            ((RelayCommand)CalibrateCommand).NotifyCanExecuteChanged();
        }
    }

    public bool SpacemouseOk
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    }

    public bool GamepadOk
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    }

    public string DeviceName
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    } = "";

    public string ErrorText
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    } = "";

    public bool ShowVigemLink
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    }

    public bool IsCalibrating
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            ((RelayCommand)CalibrateCommand).NotifyCanExecuteChanged();
        }
    }

    public double CalibProgress
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    }

    public string CalibHint
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    } = "Move device through full range when calibrating";

    public bool CalibHintUrgent
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    }

    public string SaveLabel
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
        }
    } = "";

    public bool BridgeStarting
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Notify();
            Notify(nameof(IsNotStarting));
        }
    }

    public bool IsNotStarting => !BridgeStarting;

    // ── perf ──────────────────────────────────────────────────────────────────

    public int PollRateHz
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Config.PollRateHz = value;
            Notify();
        }
    }

    public int UiRefreshHz
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Config.UiRefreshHz = value;
            _uiTimer?.Interval = TimeSpan.FromMilliseconds(1000.0 / Math.Max(1, value));
            Notify();
        }
    }

    public double CalibDurationS
    {
        get;
        set
        {
            if (field == value)
                return;
            field = value;
            Config.CalibDurationS = value;
            Notify();
        }
    }

    // ── collections ───────────────────────────────────────────────────────────

    public ObservableCollection<MeterViewModel> Meters { get; } = [];
    public ObservableCollection<AxisViewModel> AxisTabs { get; } = [];
    public ObservableCollection<AxisViewModel> MappingAxes { get; } = [];
    public ObservableCollection<ButtonMappingViewModel> Buttons { get; } = [];

    public AppConfig Config { get; }
    public static string Version => $"v{AppVersion.Current}";

    // ── commands ──────────────────────────────────────────────────────────────

    public ICommand ToggleBridgeCommand { get; }
    public ICommand CalibrateCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand OpenConfigFolderCommand { get; }
    public ICommand OpenVigemLinkCommand { get; }

    // ── internals ─────────────────────────────────────────────────────────────

    private readonly BridgeService _bridge;
    private readonly DispatcherTimer _uiTimer;
    private readonly Dispatcher _dispatcher;
    private DispatcherTimer? _saveLabelTimer;

    public MainViewModel(AppConfig config, Dispatcher dispatcher)
    {
        Config = config;
        _dispatcher = dispatcher;

        PollRateHz = config.PollRateHz;
        UiRefreshHz = config.UiRefreshHz;
        CalibDurationS = config.CalibDurationS;

        BuildCollections();

        _bridge = new BridgeService(config);
        SubscribeBridgeEvents();

        ToggleBridgeCommand = new AsyncRelayCommand(ToggleBridge);
        CalibrateCommand = new RelayCommand(Calibrate, () => IsRunning && !IsCalibrating);
        SaveCommand = new RelayCommand(Save);
        OpenLogCommand = new RelayCommand(OpenLog);
        OpenConfigFolderCommand = new RelayCommand(OpenConfigFolder);
        OpenVigemLinkCommand = new RelayCommand(OpenVigemLink);

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
        foreach (var axis in Enum.GetValues<AxisKey>())
        {
            Meters.Add(new MeterViewModel(axis.ToLabel(), axis));
            AxisTabs.Add(new AxisViewModel(axis, Config.Axes[axis]));
            MappingAxes.Add(new AxisViewModel(axis, Config.Axes[axis]));
        }

        foreach (var (key, val) in Config.Buttons.OrderBy(kv => kv.Key))
            Buttons.Add(new ButtonMappingViewModel(key, val, Config.Buttons));
    }

    // ── bridge events ─────────────────────────────────────────────────────────

    private void SubscribeBridgeEvents()
    {
        _bridge.OnError += msg => Dispatch(() => { 
            ErrorText = msg; 
            ShowVigemLink = msg.Contains("ViGEmBus"); 
            BridgeStarting = false; 
        });

        _bridge.OnGamepadReady += ok => Dispatch(() => GamepadOk = ok);
        _bridge.OnDeviceConnected += name => Dispatch(() => { 
            DeviceName = name; 
            SpacemouseOk = true; 
        });

        _bridge.OnRunning += () => Dispatch(() => { 
            IsRunning = true; 
            ErrorText = ""; 
            BridgeStarting = false; 
        });

        _bridge.OnStopped += () => Dispatch(() => { 
            IsRunning = false; 
            SpacemouseOk = false; 
            GamepadOk = false; 
            DeviceName = ""; 
        });

        _bridge.OnAxisValue += (axis, val) => Dispatch(() => GetMeter(axis)!.Value = val);
        _bridge.OnCalibProgress += p => Dispatch(() =>
        {
            CalibProgress = p;
            CalibHintUrgent = true;
            CalibHint = $"Move ALL axes to their limits! ({(int)(Config.CalibDurationS * (1 - p)) + 1}s)";
        });

        _bridge.OnCalibPeak += (axis, frac) => Dispatch(() => GetMeter(axis)!.CalibPeak = frac);
        _bridge.OnCalibComplete += _ => Dispatch(() =>
        {
            IsCalibrating = false;
            CalibProgress = 0;
            CalibHintUrgent = false;
            CalibHint = "Move device through full range when calibrating";
            foreach (var ax in AxisTabs)
                ax.RefreshScale();
            foreach (var ax in MappingAxes)
                ax.RefreshScale();
            SetCalibMode(false);
            ConfigService.Save(Config);
            ShowSave("Calibrated ✓");
        });
    }

    private MeterViewModel? GetMeter(AxisKey axis) => axis switch
    {
        AxisKey.Roll => Meters[0],
        AxisKey.Pitch => Meters[1],
        AxisKey.Yaw => Meters[2],
        AxisKey.Collective => Meters[3],
        _ => null,
    };

    private void SetCalibMode(bool on)
    {
        foreach (var m in Meters)
            m.IsCalibMode = on;
    }

    // ── commands impl ─────────────────────────────────────────────────────────

    private async Task ToggleBridge()
    {
        if (IsRunning)
        {
            await Task.Run(_bridge.Stop);
            return;
        }

        ErrorText = "";
        ShowVigemLink = false;
        BridgeStarting = true;

        bool ok = await Task.Run(_bridge.Start);
        if (!ok)
            BridgeStarting = false;
    }

    private void Calibrate()
    {
        IsCalibrating = true;
        SetCalibMode(true);
        foreach (var m in Meters)
            m.CalibPeak = 0;
        _bridge.StartCalibration();
    }

    private void Save()
    {
        ConfigService.Save(Config);
        ShowSave("Saved ✓");
    }

    private void OpenLog()
    {
        try
        {
            if (!File.Exists(Logger.FilePath))
                File.Create(Logger.FilePath).Dispose();
            Process.Start(new ProcessStartInfo("notepad.exe", Logger.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorText = $"Could not open log: {ex.Message}";
        }
    }

    private void OpenConfigFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", ConfigService.FolderPath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            ErrorText = $"Could not open folder: {ex.Message}";
        }
    }

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
        _saveLabelTimer.Tick += (_, _) => { 
            SaveLabel = ""; 
            _saveLabelTimer.Stop(); 
        };
        _saveLabelTimer.Start();
    }

    public void Shutdown()
    {
        _uiTimer.Stop();
        _bridge.Stop();
        ConfigService.Save(Config);
    }
}
