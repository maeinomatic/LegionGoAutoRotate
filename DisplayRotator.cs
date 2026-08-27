using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class DisplayRotator
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    private const uint DM_DISPLAYORIENTATION = 0x00000080;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;

    private const int DISP_CHANGE_SUCCESSFUL = 0;

    public static int GetCurrentOrientation()
    {
        var mode = GetCurrentDisplayMode();

        return checked((int)mode.dmDisplayOrientation);
    }

    public static void RotateTo(int quarterTurns)
    {
        quarterTurns &= 3;

        var mode = GetCurrentDisplayMode();

        var current = checked((int)mode.dmDisplayOrientation);

        if (current == quarterTurns)
            return;

        /*
         * 0 / 180 = same width/height relationship
         * 90 / 270 = same width/height relationship
         *
         * When moving between those groups, width and height
         * have to be swapped.
         */
        if ((current & 1) != (quarterTurns & 1))
        {
            (mode.dmPelsWidth, mode.dmPelsHeight) =
                (mode.dmPelsHeight, mode.dmPelsWidth);
        }

        mode.dmDisplayOrientation = (uint)quarterTurns;

        mode.dmFields =
            DM_DISPLAYORIENTATION |
            DM_PELSWIDTH |
            DM_PELSHEIGHT;

        var result = ChangeDisplaySettingsEx(
            null,
            ref mode,
            IntPtr.Zero,
            0,
            IntPtr.Zero);

        if (result != DISP_CHANGE_SUCCESSFUL)
        {
            throw new InvalidOperationException(
                $"ChangeDisplaySettingsEx failed with result {result}.");
        }
    }

    private static DEVMODE GetCurrentDisplayMode()
    {
        var mode = new DEVMODE
        {
            dmDeviceName = string.Empty,
            dmFormName = string.Empty
        };

        mode.dmSize = checked((ushort)Marshal.SizeOf<DEVMODE>());

        if (!EnumDisplaySettings(
                null,
                ENUM_CURRENT_SETTINGS,
                ref mode))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "EnumDisplaySettings failed.");
        }

        return mode;
    }

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumDisplaySettings(
        string? lpszDeviceName,
        int iModeNum,
        ref DEVMODE lpDevMode);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int ChangeDisplaySettingsEx(
        string? lpszDeviceName,
        ref DEVMODE lpDevMode,
        IntPtr hwnd,
        uint dwflags,
        IntPtr lParam);

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmDeviceName;

        public ushort dmSpecVersion;
        public ushort dmDriverVersion;
        public ushort dmSize;
        public ushort dmDriverExtra;

        public uint dmFields;

        public int dmPositionX;
        public int dmPositionY;

        public uint dmDisplayOrientation;
        public uint dmDisplayFixedOutput;

        public short dmColor;
        public short dmDuplex;
        public short dmYResolution;
        public short dmTTOption;
        public short dmCollate;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string dmFormName;

        public ushort dmLogPixels;

        public uint dmBitsPerPel;
        public uint dmPelsWidth;
        public uint dmPelsHeight;

        public uint dmDisplayFlags;
        public uint dmDisplayFrequency;

        public uint dmICMMethod;
        public uint dmICMIntent;
        public uint dmMediaType;
        public uint dmDitherType;

        public uint dmReserved1;
        public uint dmReserved2;

        public uint dmPanningWidth;
        public uint dmPanningHeight;
    }
}