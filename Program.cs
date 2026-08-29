using System.Windows.Forms;

internal static class Program
{
    private const string SingleInstanceMutexName =
        "Local\\maeinomatic.LegionGoAutoRotate";

    private static readonly Type LinqAssemblyMarker =
        typeof(System.Linq.Enumerable);

    [STAThread]
    private static void Main()
    {
        using var singleInstanceMutex = new Mutex(
            initiallyOwned: true,
            name: SingleInstanceMutexName,
            createdNew: out var createdNew);

        if (!createdNew)
        {
            AppLogger.Info("Second application launch detected; exiting.");
            return;
        }

        try
        {
            // WinForms ToolTip disposal uses System.Linq through a late-loaded path.
            GC.KeepAlive(LinqAssemblyMarker);

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
        catch (Exception ex)
        {
            AppLogger.Error("Unhandled application exception.", ex);
            throw;
        }
    }
}
