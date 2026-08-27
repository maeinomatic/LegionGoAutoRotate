using System.ComponentModel;
using System.Runtime.InteropServices;

internal static class DisplayRotator
{
    private const int ENUM_CURRENT_SETTINGS = -1;

    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;

    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;

    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS = 6;
    private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED = 11;
    private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED = 13;
    private const uint DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL = 0x80000000;

    private const uint DM_DISPLAYORIENTATION = 0x00000080;
    private const uint DM_PELSWIDTH = 0x00080000;
    private const uint DM_PELSHEIGHT = 0x00100000;

    private const int DISP_CHANGE_SUCCESSFUL = 0;

    public static bool TryGetCurrentOrientation(out int orientation)
    {
        if (!TryGetCurrentDisplayMode(out var mode, out _))
        {
            orientation = 0;
            return false;
        }

        orientation = checked((int)mode.dmDisplayOrientation);
        return true;
    }

    public static int GetCurrentOrientation()
    {
        if (!TryGetCurrentOrientation(out var orientation))
        {
            throw new InvalidOperationException(
                "No active internal display was found.");
        }

        return orientation;
    }

    public static bool TryRotateTo(int quarterTurns)
    {
        quarterTurns &= 3;

        if (!TryGetCurrentDisplayMode(out var mode, out var deviceName))
            return false;

        RotateDisplayMode(deviceName, ref mode, quarterTurns);

        return true;
    }

    public static void RotateTo(int quarterTurns)
    {
        if (!TryRotateTo(quarterTurns))
        {
            throw new InvalidOperationException(
                "No active internal display was found.");
        }
    }

    private static void RotateDisplayMode(
        string deviceName,
        ref DEVMODE mode,
        int quarterTurns)
    {
        quarterTurns &= 3;

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
            deviceName,
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

    private static bool TryGetCurrentDisplayMode(
        out DEVMODE mode,
        out string deviceName)
    {
        deviceName = FindActiveInternalDisplayDeviceName() ?? string.Empty;

        mode = new DEVMODE
        {
            dmDeviceName = string.Empty,
            dmFormName = string.Empty
        };

        mode.dmSize = checked((ushort)Marshal.SizeOf<DEVMODE>());

        if (string.IsNullOrWhiteSpace(deviceName))
            return false;

        if (!EnumDisplaySettings(
                deviceName,
                ENUM_CURRENT_SETTINGS,
                ref mode))
        {
            throw new Win32Exception(
                Marshal.GetLastWin32Error(),
                "EnumDisplaySettings failed.");
        }

        return true;
    }

    private static string? FindActiveInternalDisplayDeviceName()
    {
        while (true)
        {
            var result = GetDisplayConfigBufferSizes(
                QDC_ONLY_ACTIVE_PATHS,
                out var pathCount,
                out var modeCount);

            if (result != ERROR_SUCCESS)
                throw new Win32Exception(result, "GetDisplayConfigBufferSizes failed.");

            var paths = new DISPLAYCONFIG_PATH_INFO[checked((int)pathCount)];
            var modes = new DISPLAYCONFIG_MODE_INFO[checked((int)modeCount)];

            result = QueryDisplayConfig(
                QDC_ONLY_ACTIVE_PATHS,
                ref pathCount,
                paths,
                ref modeCount,
                modes,
                IntPtr.Zero);

            if (result == ERROR_INSUFFICIENT_BUFFER)
                continue;

            if (result != ERROR_SUCCESS)
                throw new Win32Exception(result, "QueryDisplayConfig failed.");

            for (var i = 0; i < checked((int)pathCount); i++)
            {
                var path = paths[i];

                if (!IsInternalOutput(path.targetInfo.outputTechnology))
                    continue;

                if (!TryGetSourceDeviceName(path.sourceInfo, out var sourceName))
                    continue;

                if (string.IsNullOrWhiteSpace(sourceName))
                    continue;

                return sourceName;
            }

            return null;
        }
    }

    private static bool TryGetSourceDeviceName(
        DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo,
        out string deviceName)
    {
        var sourceName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME
        {
            viewGdiDeviceName = string.Empty
        };
        sourceName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME;
        sourceName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_SOURCE_DEVICE_NAME>();
        sourceName.header.adapterId = sourceInfo.adapterId;
        sourceName.header.id = sourceInfo.id;

        var result = DisplayConfigGetDeviceInfo(ref sourceName);

        if (result != ERROR_SUCCESS)
        {
            deviceName = string.Empty;
            return false;
        }

        deviceName = sourceName.viewGdiDeviceName;
        return true;
    }

    private static bool IsInternalOutput(uint outputTechnology)
    {
        return outputTechnology is
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_LVDS or
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_DISPLAYPORT_EMBEDDED or
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_UDI_EMBEDDED or
            DISPLAYCONFIG_OUTPUT_TECHNOLOGY_INTERNAL;
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

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetDisplayConfigBufferSizes(
        uint flags,
        out uint numPathArrayElements,
        out uint numModeInfoArrayElements);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int QueryDisplayConfig(
        uint flags,
        ref uint numPathArrayElements,
        [Out] DISPLAYCONFIG_PATH_INFO[] pathArray,
        ref uint numModeInfoArrayElements,
        [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray,
        IntPtr currentTopologyId);

    [DllImport(
        "user32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true)]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [StructLayout(LayoutKind.Sequential)]
    private struct LUID
    {
        public uint LowPart;
        public int HighPart;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_RATIONAL
    {
        public uint Numerator;
        public uint Denominator;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINTL
    {
        public int x;
        public int y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_INFO
    {
        public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
        public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
        public uint flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_SOURCE_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_PATH_TARGET_INFO
    {
        public LUID adapterId;
        public uint id;
        public uint modeInfoIdx;
        public uint outputTechnology;
        public uint rotation;
        public uint scaling;
        public DISPLAYCONFIG_RATIONAL refreshRate;
        public uint scanLineOrdering;
        [MarshalAs(UnmanagedType.Bool)]
        public bool targetAvailable;
        public uint statusFlags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_MODE_INFO
    {
        public uint infoType;
        public uint id;
        public LUID adapterId;
        public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct DISPLAYCONFIG_MODE_INFO_UNION
    {
        [FieldOffset(0)]
        public DISPLAYCONFIG_TARGET_MODE targetMode;

        [FieldOffset(0)]
        public DISPLAYCONFIG_SOURCE_MODE sourceMode;

        [FieldOffset(0)]
        public DISPLAYCONFIG_DESKTOP_IMAGE_INFO desktopImageInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_TARGET_MODE
    {
        public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO
    {
        public ulong pixelRate;
        public DISPLAYCONFIG_RATIONAL hSyncFreq;
        public DISPLAYCONFIG_RATIONAL vSyncFreq;
        public DISPLAYCONFIG_2DREGION activeSize;
        public DISPLAYCONFIG_2DREGION totalSize;
        public uint videoStandard;
        public uint scanLineOrdering;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_SOURCE_MODE
    {
        public uint width;
        public uint height;
        public uint pixelFormat;
        public POINTL position;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DESKTOP_IMAGE_INFO
    {
        public POINTL PathSourceSize;
        public RECT DesktopImageRegion;
        public RECT DesktopImageClip;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DISPLAYCONFIG_DEVICE_INFO_HEADER
    {
        public uint type;
        public uint size;
        public LUID adapterId;
        public uint id;
    }

    [StructLayout(
        LayoutKind.Sequential,
        CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

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
