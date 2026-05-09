using System.Diagnostics;
using System.IO;

using HidSharp;

using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

using SpaceMousePilot.Extensions;
using SpaceMousePilot.Models;

namespace SpaceMousePilot.Services;

/// <summary>
/// Reads the SpaceMouse over raw HID and outputs to a virtual Xbox 360 controller.
/// All public members are thread-safe. Events are raised on the bridge thread —
/// callers must marshal to the UI thread.
/// </summary>
internal sealed class BridgeService
{
    private const int SpaceMouseVid = 0x256F;
    private const double CalibDisplayRef = 500.0;

    // Older devices: translation on report 1, rotation on report 2 (each 6 bytes)
    private static readonly Dictionary<string, (int Report, int Offset)> LegacyMap = new()
    {
        ["roll"] = (2, 1),
        ["pitch"] = (2, 3),
        ["yaw"] = (2, 5),
        ["z"] = (1, 5),
    };

    // Newer devices (Compact, Wireless): all 6 axes in report 1 as 12 bytes
    private static readonly Dictionary<string, (int Report, int Offset)> ModernMap = new()
    {
        ["roll"] = (1, 9),
        ["pitch"] = (1, 7),
        ["yaw"] = (1, 11),
        ["z"] = (1, 5),
    };

    private static readonly Dictionary<string, Xbox360Button> ButtonMap = new()
    {
        ["A"] = Xbox360Button.A,
        ["B"] = Xbox360Button.B,
        ["X"] = Xbox360Button.X,
        ["Y"] = Xbox360Button.Y,
        ["LB"] = Xbox360Button.LeftShoulder,
        ["RB"] = Xbox360Button.RightShoulder,
        ["LS"] = Xbox360Button.LeftThumb,
        ["RS"] = Xbox360Button.RightThumb,
    };

    // ── events (raised on bridge thread) ──────────────────────────────────────
    public event Action<string>? OnError;
    public event Action<bool>? OnGamepadReady;
    public event Action<string>? OnDeviceConnected;
    public event Action? OnRunning;
    public event Action? OnStopped;
    public event Action<string, double>? OnAxisValue;
    public event Action<double>? OnCalibProgress;
    public event Action<string, double>? OnCalibPeak;
    public event Action<Dictionary<string, int>>? OnCalibComplete;

    private readonly AppConfig _config;
    private Thread? _thread;
    private CancellationTokenSource? _cts;

    private volatile bool _calibrating;
    private Stopwatch? _calibTimer;
    private Dictionary<string, int> _calibPeaks = [];

    public bool IsRunning => _thread?.IsAlive == true;

    public BridgeService(AppConfig config) => _config = config;

    // ── public API ────────────────────────────────────────────────────────────

    public bool Start()
    {
        if (IsRunning)
            return false;

        if (!CanCreateViGEm())
        {
            OnError?.Invoke("ViGEmBus not found — install it and restart.");
            return false;
        }
        if (FindDevice() is null)
        {
            OnError?.Invoke("SpaceMouse not found. Check USB and close 3DxWare.");
            return false;
        }

        _cts = new CancellationTokenSource();
        _thread = new Thread(() => Run(_cts.Token)) { IsBackground = true, Name = "bridge" };
        _thread.Start();
        return true;
    }

    public void Stop()
    {
        Logger.Info("bridge", "Stop requested");
        _cts?.Cancel();
        _thread?.Join(TimeSpan.FromSeconds(3));
    }

    public void StartCalibration()
    {
        if (!IsRunning)
            return;
        _calibPeaks = _config.Axes.Keys.ToDictionary(k => k, _ => 0);
        _calibTimer = Stopwatch.StartNew();
        _calibrating = true;
        Logger.Info("bridge", $"Calibration started ({_config.CalibDurationS}s)");
    }

    // ── bridge loop ───────────────────────────────────────────────────────────

    private void Run(CancellationToken ct)
    {
        Logger.Info("bridge", "Thread started");

        // ── ViGEm ─────────────────────────────────────────────────────────────
        ViGEmClient? client = null;
        IXbox360Controller? controller = null;
        try
        {
            client = new ViGEmClient();
            controller = client.CreateXbox360Controller();
            controller.Connect();
            OnGamepadReady?.Invoke(true);
            Logger.Info("bridge", "Virtual Xbox 360 controller connected");
        }
        catch (Exception ex)
        {
            Logger.Error("bridge", ex);
            OnError?.Invoke($"Failed to create virtual gamepad:\n{ex.Message}");
            client?.Dispose();
            return;
        }

        // ── HID ───────────────────────────────────────────────────────────────
        var device = FindDevice();
        if (device is null)
        {
            OnError?.Invoke("SpaceMouse disconnected before bridge opened.");
            controller.Disconnect();
            client.Dispose();
            return;
        }

        Logger.Info("bridge", $"Opening: {device.GetFriendlyName()} PID=0x{device.ProductID:X4}");
        OnDeviceConnected?.Invoke(device.GetFriendlyName());

        HidStream? stream = null;
        try
        {
            // HidSharp 2.6.x — use new OpenConfiguration() directly
            var openCfg = new OpenConfiguration();
            stream = device.Open(openCfg);
            stream.ReadTimeout = 8;
            Logger.Info("bridge", "HID device opened");
        }
        catch (Exception ex)
        {
            Logger.Error("bridge", $"HID open failed: {ex.Message}");
            OnError?.Invoke($"Could not open SpaceMouse:\n{ex.Message}\nClose 3DxWare and retry.");
            controller.Disconnect();
            client.Dispose();
            return;
        }

        OnRunning?.Invoke();
        Logger.Info("bridge", $"Running at {_config.PollRateHz} Hz");

        var raw = new Dictionary<(int, int), short>();
        int buttons = 0;
        var axisMap = LegacyMap;
        bool fmtDetected = false;
        var buf = new byte[device.GetMaxInputReportLength()];
        var noDataSw = Stopwatch.StartNew();
        long reads = 0;
        var interval = TimeSpan.FromSeconds(1.0 / Math.Max(1, _config.PollRateHz));
        var next = Stopwatch.GetTimestamp();

        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Drain HID reports
                var latest = new Dictionary<int, byte[]>();
                while (true)
                {
                    try
                    {
                        int n = stream.Read(buf);
                        if (n > 0)
                        { latest[buf[0]] = buf[..n]; reads++; }
                    }
                    catch (TimeoutException) { break; }
                    catch (IOException ex)
                    {
                        Logger.Error("bridge", $"Read error: {ex.Message}");
                        OnError?.Invoke("SpaceMouse disconnected.");
                        return;
                    }
                }

                // Auto-detect format on first data
                if (!fmtDetected && latest.Count > 0)
                {
                    fmtDetected = true;
                    foreach (var (id, pkt) in latest)
                        Logger.Info("bridge", $"First packet — report={id} len={pkt.Length} bytes=[{string.Join(",", pkt[..Math.Min(14, pkt.Length)])}]");

                    axisMap = latest.TryGetValue(1, out var p) && p.Length >= 13
                        ? ModernMap : LegacyMap;
                    Logger.Info("bridge", $"Format: {(axisMap == ModernMap ? "MODERN" : "LEGACY")}");
                }

                if (reads == 0 && noDataSw.Elapsed.TotalSeconds > 2)
                {
                    Logger.Warn("bridge", "No HID data after 2s — possibly wrong interface");
                    noDataSw.Reset();
                }

                // Parse reports
                foreach (var (_, pkt) in latest)
                {
                    var id = pkt[0];
                    if (id is 1 or 2)
                        for (int o = 1; o + 1 < pkt.Length && o <= 11; o += 2)
                            raw[(id, o)] = (short)(pkt[o] | (pkt[o + 1] << 8));
                    else if (id == 3 && pkt.Length > 1)
                        buttons = pkt[1];
                }

                // Calibration
                if (_calibrating && _calibTimer is not null)
                {
                    var elapsed = _calibTimer.Elapsed.TotalSeconds;
                    var progress = Math.Min(1.0, elapsed / _config.CalibDurationS);
                    OnCalibProgress?.Invoke(progress);

                    foreach (var (axis, key) in axisMap)
                    {
                        var peak = Math.Abs(raw.GetValueOrDefault(key));
                        if (peak > _calibPeaks.GetValueOrDefault(axis))
                        {
                            _calibPeaks[axis] = peak;
                            OnCalibPeak?.Invoke(axis, Math.Min(1.0, peak / CalibDisplayRef));
                        }
                    }

                    if (elapsed >= _config.CalibDurationS)
                    {
                        _calibrating = false;
                        foreach (var (axis, peak) in _calibPeaks)
                            if (peak > 10)
                                _config.Axes[axis].Scale = peak;
                        Logger.Info("bridge", $"Calibration complete: {string.Join(", ", _calibPeaks.Select(kv => $"{kv.Key}={kv.Value}"))}");
                        OnCalibComplete?.Invoke(new Dictionary<string, int>(_calibPeaks));
                    }
                }

                // Process axes
                foreach (var (axis, axCfg) in _config.Axes)
                {
                    if (!axisMap.TryGetValue(axis, out var key))
                        continue;
                    var val = Process(raw.GetValueOrDefault(key), axCfg);
                    OnAxisValue?.Invoke(axis, val);
                    SetAxis(controller, axCfg.Gamepad, val);
                }

                // Buttons
                foreach (var (idxStr, label) in _config.Buttons)
                    if (int.TryParse(idxStr, out int idx) && ButtonMap.TryGetValue(label, out var btn))
                        controller.SetButtonState(btn, ((buttons >> idx) & 1) == 1);

                controller.SubmitReport();

                // Pace to poll rate
                next += (long)(interval.TotalSeconds * Stopwatch.Frequency);
                var wait = next - Stopwatch.GetTimestamp();
                if (wait > 0)
                    Thread.Sleep((int)(wait * 1000.0 / Stopwatch.Frequency));
            }
        }
        catch (Exception ex)
        {
            Logger.Error("bridge", $"Loop crashed: {ex}");
            OnError?.Invoke($"Bridge crashed:\n{ex.Message}");
        }
        finally
        {
            stream?.Close();
            controller.Disconnect();
            client.Dispose();
            OnStopped?.Invoke();
            Logger.Info("bridge", "Thread exited");
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static HidDevice? FindDevice()
    {
        var devs = DeviceList.Local.GetHidDevices(vendorID: SpaceMouseVid).ToList();
        if (devs.Count == 0)
        {
            Logger.Warn("bridge", $"No devices for VID=0x{SpaceMouseVid:X4}");
            Logger.Debug("bridge", $"All HID: {string.Join(", ", DeviceList.Local.GetHidDevices().Select(d => $"0x{d.VendorID:X4}/0x{d.ProductID:X4} {d.SafeName()}"))}");
            return null;
        }

        Logger.Info("bridge", $"Found {devs.Count} SpaceMouse interface(s)");
        foreach (var d in devs)
            Logger.Debug("bridge", $"  PID=0x{d.ProductID:X4} {d.GetFriendlyName()}");

        // Prefer Generic Desktop / Multi-axis Controller (usage page 1, usage 8)
        // via report descriptor — GetUsagePage/GetUsage removed in 2.6.x
        foreach (var d in devs)
        {
            try
            {
                var descriptor = d.GetReportDescriptor();
                foreach (var deviceItem in descriptor.DeviceItems)
                {
                    if (deviceItem.Usages.GetAllValues().Any(u => u == 0x00010008))
                    {
                        Logger.Info("bridge", $"Selected (usage match): PID=0x{d.ProductID:X4}");
                        return d;
                    }
                }
            }
            catch { }
        }

        Logger.Info("bridge", $"Selected (fallback): PID=0x{devs[0].ProductID:X4}");
        return devs[0];
    }

    private static bool CanCreateViGEm()
    {
        try
        { using var c = new ViGEmClient(); return true; }
        catch { return false; }
    }

    private static void SetAxis(IXbox360Controller ctrl, string gamepad, double val)
    {
        short s = (short)(val * short.MaxValue);
        switch (gamepad)
        {
            case "left_x":
                ctrl.SetAxisValue(Xbox360Axis.LeftThumbX, s);
                break;
            case "left_y":
                ctrl.SetAxisValue(Xbox360Axis.LeftThumbY, s);
                break;
            case "right_x":
                ctrl.SetAxisValue(Xbox360Axis.RightThumbX, s);
                break;
            case "right_y":
                ctrl.SetAxisValue(Xbox360Axis.RightThumbY, s);
                break;
            case "lt":
                ctrl.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(Math.Max(0, val) * 255));
                break;
            case "rt":
                ctrl.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(Math.Max(0, val) * 255));
                break;
        }
    }

    private static double Process(short raw, AxisConfig cfg)
    {
        double val = Math.Max(-1.0, Math.Min(1.0, raw / (double)Math.Max(1, cfg.Scale)));
        double dz = cfg.Deadzone;
        if (Math.Abs(val) < dz)
            return 0.0;
        val = Math.CopySign((Math.Abs(val) - dz) / (1.0 - dz), val);
        val = Math.CopySign(Math.Pow(Math.Abs(val), cfg.Curve), val) * cfg.Sensitivity;
        return cfg.Invert ? Math.Max(-1.0, Math.Min(1.0, -val))
                          : Math.Max(-1.0, Math.Min(1.0, val));
    }
}
