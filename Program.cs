using System.Windows.Forms;

internal static class Program
{
    private static readonly Type LinqAssemblyMarker =
        typeof(System.Linq.Enumerable);

    [STAThread]
    private static void Main()
    {
        // WinForms ToolTip disposal uses System.Linq through a late-loaded path.
        GC.KeepAlive(LinqAssemblyMarker);

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApplicationContext());
    }
}
