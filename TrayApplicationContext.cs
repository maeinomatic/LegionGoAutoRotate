using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AutoRotateController _autoRotateController;
    private readonly LegionControllerDockMonitor _controllerDockMonitor;
    private readonly AppSettings _settings;
    private readonly SynchronizationContext? _uiContext;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly ToolStripMenuItem _rotateWithControllersAttachedMenuItem;

    private bool _autoRotateRequested = true;

    public TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current;
        _autoRotateController = new AutoRotateController();
        _controllerDockMonitor = new LegionControllerDockMonitor();
        _controllerDockMonitor.DockStateChanged += ControllerDockStateChanged;
        _settings = AppSettingsStore.Load();

        _startMenuItem = new ToolStripMenuItem(
            "Start Auto Rotate",
            null,
            (_, _) => StartAutoRotate());

        _stopMenuItem = new ToolStripMenuItem(
            "Stop Auto Rotate",
            null,
            (_, _) => StopAutoRotate());

        var exitMenuItem = new ToolStripMenuItem(
            "Exit",
            null,
            (_, _) => ExitApplication());

        var openDiagnosticsMenuItem = new ToolStripMenuItem(
            "Open Diagnostics Folder",
            null,
            (_, _) => OpenDiagnosticsFolder());

        _startWithWindowsMenuItem = new ToolStripMenuItem(
            "Start with Windows",
            null,
            (_, _) => ToggleStartWithWindows())
        {
            CheckOnClick = false,
            Checked = StartupRegistration.IsEnabled()
        };

        _rotateWithControllersAttachedMenuItem = new ToolStripMenuItem(
            "Rotate with Controllers Attached",
            null,
            (_, _) => ToggleRotateWithControllersAttached())
        {
            CheckOnClick = false,
            Checked = _settings.RotateWithControllersAttached
        };

        _contextMenu = new ContextMenuStrip();

        _contextMenu.Items.Add(_startMenuItem);
        _contextMenu.Items.Add(_stopMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(_rotateWithControllersAttachedMenuItem);
        _contextMenu.Items.Add(_startWithWindowsMenuItem);
        _contextMenu.Items.Add(openDiagnosticsMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Legion Go Auto Rotate",
            ContextMenuStrip = _contextMenu,
            Visible = true
        };

        /*
         * Starting the application means auto-rotation starts.
         *
         * This has nothing to do with Windows startup.
         * There is no Windows-startup functionality in this version.
         */
        ApplyAutoRotatePolicy();
    }

    private void StartAutoRotate()
    {
        _autoRotateRequested = true;
        ApplyAutoRotatePolicy();
    }

    private void StartAutoRotateNow()
    {
        if (_autoRotateController.IsRunning)
            return;

        var started = _autoRotateController.Start();

        if (!started)
        {
            AppLogger.ThrottledError(
                "auto-rotate-start-failed",
                TimeSpan.FromMinutes(5),
                "Auto-rotation could not be started.");

            MessageBox.Show(
                "Windows did not expose a SimpleOrientationSensor.",
                "Legion Go Auto Rotate",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private static void OpenDiagnosticsFolder()
    {
        try
        {
            Directory.CreateDirectory(AppLogger.LogDirectory);

            Process.Start(new ProcessStartInfo
            {
                FileName = AppLogger.LogDirectory,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open diagnostics folder.", ex);
        }
    }

    private void ToggleStartWithWindows()
    {
        var enable = !_startWithWindowsMenuItem.Checked;

        try
        {
            StartupRegistration.SetEnabled(enable);
            _startWithWindowsMenuItem.Checked = StartupRegistration.IsEnabled();
        }
        catch
        {
            _startWithWindowsMenuItem.Checked = StartupRegistration.IsEnabled();

            MessageBox.Show(
                "Windows startup registration could not be updated. See diagnostics for details.",
                "Legion Go Auto Rotate",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void ToggleRotateWithControllersAttached()
    {
        _settings.RotateWithControllersAttached =
            !_settings.RotateWithControllersAttached;

        AppSettingsStore.Save(_settings);
        ApplyAutoRotatePolicy();
    }

    private void StopAutoRotate()
    {
        _autoRotateRequested = false;
        _autoRotateController.Stop();

        UpdateMenuState();
    }

    private void ControllerDockStateChanged(
        object? sender,
        ControllerDockStateChangedEventArgs e)
    {
        if (_uiContext is not null)
        {
            _uiContext.Post(_ => ApplyAutoRotatePolicy(), null);
            return;
        }

        ApplyAutoRotatePolicy();
    }

    private void ApplyAutoRotatePolicy()
    {
        var controllersBlockRotation =
            !_settings.RotateWithControllersAttached &&
            _controllerDockMonitor.CurrentState.BothDocked;

        if (_autoRotateRequested && !controllersBlockRotation)
        {
            StartAutoRotateNow();
        }
        else
        {
            _autoRotateController.Stop();
        }

        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        _startMenuItem.Enabled =
            !_autoRotateRequested;

        _stopMenuItem.Enabled =
            _autoRotateRequested;

        _startWithWindowsMenuItem.Checked =
            StartupRegistration.IsEnabled();

        _rotateWithControllersAttachedMenuItem.Checked =
            _settings.RotateWithControllersAttached;
    }

    private void ExitApplication()
    {
        _autoRotateController.Stop();

        ExitThread();
    }

    protected override void ExitThreadCore()
    {
        _notifyIcon.Visible = false;

        _notifyIcon.Dispose();
        _contextMenu.Dispose();
        _autoRotateController.Dispose();
        _controllerDockMonitor.Dispose();

        base.ExitThreadCore();
    }
}
