using System.IO;
using System.IO.Pipes;
using System.Windows;
using System.Windows.Threading;

using Hardcodet.Wpf.TaskbarNotification;

using SpaceMousePilot.Services;
using SpaceMousePilot.ViewModels;
using SpaceMousePilot.Views;

using Application = System.Windows.Application;

namespace SpaceMousePilot;

public sealed partial class App : Application
{
    private const string _mutexName = "SpaceMousePilot_SingleInstance";
    private const string _pipeName = "SpaceMousePilot_IPC";

    private Mutex? _mutex;
    private bool _ownsMutex;
    private TaskbarIcon? _tray;
    private MainWindow? _window;
    private MainViewModel? _vm;
    private CancellationTokenSource? _pipeCts;

    protected override void OnStartup(StartupEventArgs e)
    {
        AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += TaskScheduler_UnobservedTaskException;

        try
        {
            base.OnStartup(e);

            _mutex = new Mutex(true, _mutexName, out bool isNew);
            _ownsMutex = isNew;
            if (!isNew)
            {
                SignalRunningInstance();
                Shutdown();
                return;
            }

            bool startMinimized = e.Args.Contains("--tray");

            _ = Task.Factory.StartNew(
                ListenForIpc,
                TaskCreationOptions.LongRunning);

            var config = ConfigService.Load();
            _vm = new MainViewModel(config, Dispatcher);
            _window = new MainWindow(_vm);

            SetupTray();

            MainWindow = _window;

            if (!startMinimized)
                _window.Show();
        }
        catch (Exception ex)
        {
            try
            {
                Logger.Error("startup", ex.ToString());
            }
            catch { }

            MessageBox.Show(
                ex.ToString(),
                "Startup Failure",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            Shutdown(-1);
        }
    }

    private void TaskScheduler_UnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        try
        {
            Logger.Error("crash", e.Exception.ToString());
        }
        catch { }

        e.SetObserved();
    }

    private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        try
        {
            Logger.Error("crash", e.Exception.ToString());
        }
        catch { }

        MessageBox.Show(
            e.Exception.Message,
            "Unhandled UI Exception",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        e.Handled = true;
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (e.ExceptionObject is Exception ex)
        {
            try
            {
                Logger.Error("crash", ex.ToString());
            }
            catch { }
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _pipeCts?.Cancel();
        _tray?.Dispose();
        if (_ownsMutex)
            _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }

    // ── tray ──────────────────────────────────────────────────────────────────

    private void SetupTray()
    {
        _tray = (TaskbarIcon)FindResource("TrayIcon");

        var menu = new System.Windows.Controls.ContextMenu();

        var openItem = new System.Windows.Controls.MenuItem { Header = "Open" };
        openItem.Click += (_, _) => ShowWindow();

        var sep = new System.Windows.Controls.Separator();

        var quitItem = new System.Windows.Controls.MenuItem { Header = "Quit" };
        quitItem.Click += (_, _) => DoQuit();

        menu.Items.Add(openItem);
        menu.Items.Add(sep);
        menu.Items.Add(quitItem);

        _tray.ContextMenu = menu;
        _tray.TrayMouseDoubleClick += (_, _) => ShowWindow();
    }

    private void ShowWindow()
    {
        if (_window is null)
            return;
        _window.Show();
        _window.WindowState = WindowState.Normal;
        _window.Activate();
    }

    private void DoQuit()
    {
        _vm?.Shutdown();
        Shutdown();
    }

    // ── IPC ───────────────────────────────────────────────────────────────────

    private static void SignalRunningInstance()
    {
        try
        {
            using var client = new NamedPipeClientStream(".", _pipeName, PipeDirection.Out);
            client.Connect(500);
            using var w = new StreamWriter(client);
            w.Write("SHOW");
        }
        catch (Exception ex)
        {
            Logger.Error("signal ipc", ex.ToString());
        }
    }

    private void ListenForIpc()
    {
        _pipeCts = new CancellationTokenSource();
        while (!_pipeCts.Token.IsCancellationRequested)
        {
            try
            {
                using var server = new NamedPipeServerStream(_pipeName, PipeDirection.In);
                server.WaitForConnection();
                using var r = new StreamReader(server);
                if (r.ReadToEnd() == "SHOW")
                    Dispatcher.Invoke(ShowWindow);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Error("listen ipc", ex.ToString());
            }
        }
    }
}
