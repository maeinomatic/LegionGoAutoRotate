using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly AutoRotateController _autoRotateController;
    private readonly LegionControllerDockMonitor _controllerDockMonitor;
    private readonly AppSettings _settings;
    private readonly SynchronizationContext? _uiContext;
    private readonly Icon _activeIcon;
    private readonly Icon _pausedIcon;
    private readonly Icon _controllerBlockedIcon;

    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;

    private readonly ToolStripMenuItem _startMenuItem;
    private readonly ToolStripMenuItem _stopMenuItem;
    private readonly ToolStripMenuItem _startWithWindowsMenuItem;
    private readonly ToolStripMenuItem _rotateWithControllersAttachedMenuItem;

    private bool _autoRotateRequested = true;
    private bool _controllersBlockRotation;

    public TrayApplicationContext()
    {
        _uiContext = SynchronizationContext.Current;
        _autoRotateController = new AutoRotateController();
        _controllerDockMonitor = new LegionControllerDockMonitor();
        _controllerDockMonitor.DockStateChanged += ControllerDockStateChanged;
        _settings = AppSettingsStore.Load();
        _activeIcon = TrayIconLoader.Load("TrayActive");
        _pausedIcon = TrayIconLoader.Load("TrayPaused");
        _controllerBlockedIcon = TrayIconLoader.Load("TrayControllerBlocked");

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

        var aboutMenuItem = new ToolStripMenuItem(
            "About Legion Go Auto Rotate",
            null,
            (_, _) => ShowAbout());

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
        _contextMenu.Items.Add(aboutMenuItem);
        _contextMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = _pausedIcon,
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
        _controllersBlockRotation =
            !_settings.RotateWithControllersAttached &&
            (!_controllerDockMonitor.HasReceivedControllerReport ||
                _controllerDockMonitor.CurrentState.AnyDocked);

        if (_autoRotateRequested && !_controllersBlockRotation)
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

        UpdateTrayIcon();
    }

    private void UpdateTrayIcon()
    {
        if (_autoRotateRequested && _controllersBlockRotation)
        {
            _notifyIcon.Icon = _controllerBlockedIcon;
            _notifyIcon.Text = "Legion Go Auto Rotate - Controllers Attached";
            return;
        }

        if (_autoRotateRequested && _autoRotateController.IsRunning)
        {
            _notifyIcon.Icon = _activeIcon;
            _notifyIcon.Text = "Legion Go Auto Rotate - Active";
            return;
        }

        _notifyIcon.Icon = _pausedIcon;
        _notifyIcon.Text = "Legion Go Auto Rotate - Paused";
    }

    private void ShowAbout()
    {
        var version =
            Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ??
            "unknown";

        using var dialog = new AboutDialog(
            version,
            GetCurrentStateDescription());

        dialog.ShowDialog();
    }

    private string GetCurrentStateDescription()
    {
        if (_autoRotateRequested && _controllersBlockRotation)
            return "Paused because controllers are attached";

        if (_autoRotateRequested && _autoRotateController.IsRunning)
            return "Active";

        return "Paused";
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
        _activeIcon.Dispose();
        _pausedIcon.Dispose();
        _controllerBlockedIcon.Dispose();

        base.ExitThreadCore();
    }
}

internal sealed class AboutDialog : Form
{
    private const string RepositoryUrl =
        "https://github.com/maeinomatic/LegionGoAutoRotate";

    public AboutDialog(string version, string currentState)
    {
        Text = "About Legion Go Auto Rotate";
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowIcon = true;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterScreen;
        Padding = new Padding(14);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            Dock = DockStyle.Fill
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var titleLabel = new Label
        {
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 10),
            Text = "Legion Go Auto Rotate"
        };

        var versionLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Text = $"Version {version}"
        };

        var descriptionLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Text = "Automatic screen rotation for Lenovo Legion Go 2"
        };

        var repositoryLink = new LinkLabel
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 8),
            Text = RepositoryUrl
        };
        repositoryLink.LinkClicked += (_, _) => OpenRepository();

        var stateLabel = new Label
        {
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18),
            Text = $"Current state: {currentState}"
        };

        var okButton = new Button
        {
            Anchor = AnchorStyles.Right,
            AutoSize = true,
            DialogResult = DialogResult.OK,
            Margin = new Padding(0),
            MinimumSize = new Size(76, 28),
            Text = "OK"
        };

        layout.Controls.Add(titleLabel);
        layout.Controls.Add(versionLabel);
        layout.Controls.Add(descriptionLabel);
        layout.Controls.Add(repositoryLink);
        layout.Controls.Add(stateLabel);
        layout.Controls.Add(okButton);

        Controls.Add(layout);

        AcceptButton = okButton;
        CancelButton = okButton;
    }

    private static void OpenRepository()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = RepositoryUrl,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            AppLogger.Error("Failed to open repository URL.", ex);
        }
    }
}

internal static class TrayIconLoader
{
    public static Icon Load(string resourceName)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                AppLogger.Error($"Tray icon resource '{resourceName}' was not found.");
                return (Icon)SystemIcons.Application.Clone();
            }

            using var bitmap = new Bitmap(stream);
            using var trayBitmap = RenderTrayBitmap(bitmap);
            var handle = trayBitmap.GetHicon();

            try
            {
                return (Icon)Icon.FromHandle(handle).Clone();
            }
            finally
            {
                DestroyIcon(handle);
            }
        }
        catch (Exception ex)
        {
            AppLogger.Error($"Failed to load tray icon resource '{resourceName}'.", ex);
            return (Icon)SystemIcons.Application.Clone();
        }
    }

    private static Bitmap RenderTrayBitmap(Bitmap source)
    {
        var size = SystemInformation.SmallIconSize;
        var bitmap = new Bitmap(size.Width, size.Height);

        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.CompositingQuality = CompositingQuality.HighQuality;
        graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
        graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
        graphics.SmoothingMode = SmoothingMode.HighQuality;

        graphics.DrawImage(
            source,
            new Rectangle(Point.Empty, size),
            new Rectangle(0, 0, source.Width, source.Height),
            GraphicsUnit.Pixel);

        return bitmap;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
