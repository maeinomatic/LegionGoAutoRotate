using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AutoRotateController _autoRotateController;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;

    public TrayApplicationContext()
    {
        _autoRotateController = new AutoRotateController();
        AppSettingsStore.Load();

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

        _contextMenu = new ContextMenuStrip();

        _contextMenu.Items.Add(_startMenuItem);
        _contextMenu.Items.Add(_stopMenuItem);
        _contextMenu.Items.Add(new ToolStripSeparator());
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
        StartAutoRotate();
    }

    private void StartAutoRotate()
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

        UpdateMenuState();
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

    private void StopAutoRotate()
    {
        _autoRotateController.Stop();

        UpdateMenuState();
    }

    private void UpdateMenuState()
    {
        _startMenuItem.Enabled =
            !_autoRotateController.IsRunning;

        _stopMenuItem.Enabled =
            _autoRotateController.IsRunning;

        _startWithWindowsMenuItem.Checked =
            StartupRegistration.IsEnabled();
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

        base.ExitThreadCore();
    }
}
