using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

using HidSharp;

using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;

using SpaceMousePilot.Enums;
using SpaceMousePilot.Extensions;
using SpaceMousePilot.Models;

namespace SpaceMousePilot.Services;

/// <summary>
/// Reads the SpaceMouse over raw HID and outputs to a virtual Xbox 360 controller.
/// All public members are thread-safe. Events are raised on the bridge thread —
/// callers must marshal to the UI thread.
/// </summary>
internal sealed class BridgeService(AppConfig config)
{
    private readonly AppConfig _config = config;

    private const int _spaceMouseVid = 0x256F;
    private const double _calibDisplayRef = 500.0;

    // (report id, byte offset) for each axis
    private static readonly Dictionary<AxisKey, (int Report, int Offset)> _legacyMap = new()
    {
        [AxisKey.Roll] = (2, 1),
        [AxisKey.Pitch] = (2, 3),
        [AxisKey.Yaw] = (2, 5),
        [AxisKey.Collective] = (1, 5),
    };

    private static readonly Dictionary<AxisKey, (int Report, int Offset)> _modernMap = new()
    {
        [AxisKey.Roll] = (1, 9),
        [AxisKey.Pitch] = (1, 7),
        [AxisKey.Yaw] = (1, 11),
        [AxisKey.Collective] = (1, 5),
    };

    private static readonly Dictionary<GamepadButton, Xbox360Button> _buttonMap = new()
    {
        [GamepadButton.A] = Xbox360Button.A,
        [GamepadButton.B] = Xbox360Button.B,
        [GamepadButton.X] = Xbox360Button.X,
        [GamepadButton.Y] = Xbox360Button.Y,
        [GamepadButton.LB] = Xbox360Button.LeftShoulder,
        [GamepadButton.RB] = Xbox360Button.RightShoulder,
        [GamepadButton.LS] = Xbox360Button.LeftThumb,
        [GamepadButton.RS] = Xbox360Button.RightThumb,
    };

    // ── events (raised on bridge thread) ──────────────────────────────────────
    public event Action<string>? OnError;
    public event Action<bool>? OnGamepadReady;
    public event Action<string>? OnDeviceConnected;
    public event Action? OnRunning;
    public event Action? OnStopped;
    public event Action<double>? OnCalibProgress;
    public event Action<AxisKey, double>? OnCalibPeak;
    public event Action<Dictionary<AxisKey, int>>? OnCalibComplete;

    private Thread? _thread;
    private CancellationTokenSource? _cts;

    private volatile bool _calibrating;
    private Stopwatch? _calibTimer;
    private Dictionary<AxisKey, int> _calibPeaks = [];

    public bool IsRunning => _thread?.IsAlive == true;

    private readonly double[] _latestAxisValues = new double[Enum.GetValues<AxisKey>().Length]; 
    private readonly double[] _filteredAxisValues = new double[Enum.GetValues<AxisKey>().Length];

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

        _calibPeaks = Enum.GetValues<AxisKey>().ToDictionary(k => k, _ => 0);
        _calibTimer = Stopwatch.StartNew();
        _calibrating = true;
        Logger.Info("bridge", $"Calibration started ({_config.CalibDurationS}s)");
    }

    // ── bridge loop ───────────────────────────────────────────────────────────

    private void Run(CancellationToken ct)
    {
        Logger.Info("bridge", "Thread started");

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

            OnError?.Invoke(
                $"Failed to create virtual gamepad:\n{ex.Message}");

            client?.Dispose();
            return;
        }

        var device = FindDevice();

        if (device is null)
        {
            OnError?.Invoke(
                "SpaceMouse disconnected before bridge opened.");

            controller.Disconnect();
            client.Dispose();

            return;
        }

        Logger.Info(
            "bridge",
            $"Opening: {device.GetFriendlyName()} PID=0x{device.ProductID:X4}");

        OnDeviceConnected?.Invoke(device.GetFriendlyName());

        HidStream? stream = null;

        try
        {
            stream = device.Open(new OpenConfiguration());
            stream.ReadTimeout = 1;

            Logger.Info("bridge", "HID device opened");
        }
        catch (Exception ex)
        {
            Logger.Error("bridge", $"HID open failed: {ex.Message}");

            OnError?.Invoke(
                $"Could not open SpaceMouse:\n{ex.Message}\nClose 3DxWare and retry.");

            controller.Disconnect();
            client.Dispose();

            return;
        }

        OnRunning?.Invoke();

        Logger.Info("bridge", $"Running at {_config.PollRateHz} Hz");

        short[] report1 = new short[6];
        short[] report2 = new short[6];

        int buttons = 0;

        var axisMap = _legacyMap;

        bool fmtDetected = false;

        var buf = new byte[device.GetMaxInputReportLength()];

        double freq = Stopwatch.Frequency;
        long last = Stopwatch.GetTimestamp();
        long ticksPerFrame = Stopwatch.Frequency / Math.Max(1, _config.PollRateHz);
        long next = Stopwatch.GetTimestamp();

        double dt = 1.0 / Math.Max(1, _config.PollRateHz);

        try
        {
            while (!ct.IsCancellationRequested)
            {
                UpdatePollRate(ref ticksPerFrame);

                // --- dt calculation (time-based, independent of Hz stability)
                long now = Stopwatch.GetTimestamp();
                dt = (now - last) / freq;
                last = now;

                dt = Math.Min(dt, 0.05);

                ReadReports(
                    stream,
                    buf,
                    report1,
                    report2,
                    ref buttons,
                    ref fmtDetected,
                    ref axisMap);

                ProcessCalibration(
                    axisMap,
                    report1,
                    report2);

                ProcessAxes(
                    controller,
                    axisMap,
                    report1,
                    report2,
                    dt);

                ProcessButtons(
                    controller,
                    buttons);

                controller.SubmitReport();

                WaitForNextFrame(
                    ref next,
                    ticksPerFrame);
            }
        }
        catch (Exception ex)
        {
            Logger.Error("bridge", $"Loop crashed: {ex}");

            OnError?.Invoke(
                $"Bridge crashed:\n{ex.Message}");
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
        var devs = DeviceList.Local.GetHidDevices(vendorID: _spaceMouseVid).ToList();
        if (devs.Count == 0)
        {
            Logger.Warn("bridge", $"No devices for VID=0x{_spaceMouseVid:X4}");
            Logger.Debug("bridge", $"All HID: \n{string.Join("\n", DeviceList.Local.GetHidDevices().Select(d => $"0x{d.VendorID:X4}/0x{d.ProductID:X4} {d.SafeName()}"))}\n\n");
            return null;
        }

        Logger.Info("bridge", $"Found {devs.Count} SpaceMouse interface(s)");
        foreach (var d in devs)
            Logger.Debug("bridge", $"  PID=0x{d.ProductID:X4} {d.GetFriendlyName()}");

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
        {
            using var c = new ViGEmClient();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SetAxis(IXbox360Controller ctrl, GamepadAxis axis, double val)
    {
        short s = (short)(val * short.MaxValue);
        switch (axis)
        {
            case GamepadAxis.left_x:
                ctrl.SetAxisValue(Xbox360Axis.LeftThumbX, s);
                break;
            case GamepadAxis.left_y:
                ctrl.SetAxisValue(Xbox360Axis.LeftThumbY, s);
                break;
            case GamepadAxis.right_x:
                ctrl.SetAxisValue(Xbox360Axis.RightThumbX, s);
                break;
            case GamepadAxis.right_y:
                ctrl.SetAxisValue(Xbox360Axis.RightThumbY, s);
                break;
            case GamepadAxis.lt:
                ctrl.SetSliderValue(Xbox360Slider.LeftTrigger, (byte)(Math.Max(0, val) * 255));
                break;
            case GamepadAxis.rt:
                ctrl.SetSliderValue(Xbox360Slider.RightTrigger, (byte)(Math.Max(0, val) * 255));
                break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void UpdatePollRate(ref long ticksPerFrame)
    {
        int hz = _config.PollRateHz;

        long newTicks = Stopwatch.Frequency / (hz > 0 ? hz : 1);

        if (newTicks != ticksPerFrame)
            ticksPerFrame = newTicks;
    }

    private void ReadReports(
        HidStream stream,
        byte[] buf,
        short[] report1,
        short[] report2,
        ref int buttons,
        ref bool fmtDetected,
        ref Dictionary<AxisKey, (int Report, int Offset)> axisMap)
    {
        while (true)
        {
            try
            {
                int n = stream.Read(buf);

                if (n <= 0)
                    break;

                byte id = buf[0];

                //
                // Detect packet format once
                //
                if (!fmtDetected)
                {
                    fmtDetected = true;

                    axisMap = (id == 1 && n >= 13)
                        ? _modernMap
                        : _legacyMap;

                    Logger.Info( "bridge", $"Format: {(axisMap == _modernMap ? "MODERN" : "LEGACY")}");
                }

                //
                // Motion reports
                //
                if (id is 1 or 2)
                {
                    short[] target = id == 1
                        ? report1
                        : report2;

                    int idx = 0;

                    for (int o = 1; o + 1 < n && idx < 6; o += 2)
                    {
                        target[idx++] = (short)(buf[o] | (buf[o + 1] << 8));
                    }
                }
                //
                // Button report
                //
                else if (id == 3 && n > 1)
                {
                    buttons = buf[1];
                }
            }
            catch (TimeoutException)
            {
                break;
            }
            catch (IOException ex)
            {
                Logger.Error("bridge", $"Read error: {ex.Message}");

                OnError?.Invoke("SpaceMouse disconnected.");

                throw;
            }
        }
    }

    private void ProcessCalibration(
        Dictionary<AxisKey, (int Report, int Offset)> axisMap,
        short[] report1,
        short[] report2)
    {
        if (!_calibrating || _calibTimer is null)
            return;

        double elapsed = _calibTimer.Elapsed.TotalSeconds;
        double progress = elapsed / _config.CalibDurationS;

        if (progress > 1.0)
            progress = 1.0;

        OnCalibProgress?.Invoke(progress);

        foreach (var (axis, key) in axisMap)
        {
            short raw = key.Report == 1
                ? report1[key.Offset >> 1]
                : report2[key.Offset >> 1];

            int peak = raw >= 0
                ? raw
                : -raw;

            if (peak > _calibPeaks[axis])
            {
                _calibPeaks[axis] = peak;

                double normalized = peak / _calibDisplayRef;

                if (normalized > 1.0)
                    normalized = 1.0;

                OnCalibPeak?.Invoke(axis, normalized);
            }
        }

        if (elapsed < _config.CalibDurationS)
            return;

        _calibrating = false;

        foreach (var (axis, peak) in _calibPeaks)
        {
            if (peak > 10)
                _config.Axes[axis].Scale = peak;
        }

        Logger.Info("bridge", "Calibration complete");

        OnCalibComplete?.Invoke(new Dictionary<AxisKey, int>(_calibPeaks));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ProcessAxes(
    IXbox360Controller controller,
    Dictionary<AxisKey, (int Report, int Offset)> axisMap,
    short[] report1,
    short[] report2,
    double dt)
    {
        double tau = _config.FilterTauS;

        // convert tau → alpha once per frame
        double alpha =
            tau <= 0.000001
                ? 1.0
                : 1.0 - Math.Exp(-dt / tau);

        foreach (var (axis, cfg) in _config.Axes)
        {
            if (!axisMap.TryGetValue(axis, out var key))
                continue;

            short raw = key.Report == 1
                ? report1[key.Offset >> 1]
                : report2[key.Offset >> 1];

            double target = Process(raw, cfg);

            int i = (int)axis;

            double prev = _filteredAxisValues[i];

            double filtered = prev + alpha * (target - prev);

            _filteredAxisValues[i] = filtered;

            SetAxis(controller, cfg.Gamepad, filtered);
        }
    }

    private void ProcessButtons(
    IXbox360Controller controller,
    int buttons)
    {
        foreach (var (idxStr, btn) in _config.Buttons)
        {
            if (!int.TryParse(idxStr, out int idx))
                continue;

            if (!_buttonMap.TryGetValue(btn, out var xBtn))
                continue;

            bool pressed = ((buttons >> idx) & 1) != 0;

            controller.SetButtonState(xBtn, pressed);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WaitForNextFrame(
        ref long next,
        long ticksPerFrame)
    {
        next += ticksPerFrame;

        long wait = next - Stopwatch.GetTimestamp();

        if (wait > 0)
        {
            int sleepMs = (int)(wait * 1000 / Stopwatch.Frequency);

            //
            // Coarse sleep first
            //
            if (sleepMs > 1)
                Thread.Sleep(sleepMs - 1);

            //
            // Fine-grained spin
            //
            while (Stopwatch.GetTimestamp() < next)
                Thread.SpinWait(16);
        }
        else
        {
            //
            // Drift correction
            //
            next = Stopwatch.GetTimestamp();
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static double Process(short raw, AxisConfig cfg)
    {
        double scale = cfg.Scale > 0 ? cfg.Scale : 1.0;

        double val = raw / scale;

        // clamp
        val = val > 1.0 ? 1.0 : (val < -1.0 ? -1.0 : val);

        // deadzone uses magnitude but keeps sign separate
        double sign = val < 0 ? -1.0 : 1.0;
        double abs = val < 0 ? -val : val;

        double dz = cfg.Deadzone;

        if (abs < dz)
            return 0.0;

        // normalize after deadzone
        val = (abs - dz) / (1.0 - dz);

        // curve on magnitude
        if (cfg.Curve != 1.0)
            val = Math.Pow(val, cfg.Curve);

        // apply sensitivity + sign
        val = val * cfg.Sensitivity * sign;

        // invert
        if (cfg.Invert)
            val = -val;

        // final clamp
        return val > 1.0 ? 1.0 : (val < -1.0 ? -1.0 : val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public double GetAxisValue(AxisKey axis)  => Volatile.Read(ref _latestAxisValues[(int)axis]);
}
