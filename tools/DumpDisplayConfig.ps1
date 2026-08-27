Add-Type -TypeDefinition @"
using System;
using System.ComponentModel;
using System.Runtime.InteropServices;

public static class DisplayConfigDump
{
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_INSUFFICIENT_BUFFER = 122;
    private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_SOURCE_NAME = 1;
    private const uint DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME = 2;

    public static void Dump()
    {
        while (true)
        {
            uint pathCount;
            uint modeCount;
            var result = GetDisplayConfigBufferSizes(
                QDC_ONLY_ACTIVE_PATHS,
                out pathCount,
                out modeCount);

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

            for (var i = 0; i < pathCount; i++)
            {
                var path = paths[i];
                var sourceName = GetSourceName(path.sourceInfo);
                var targetName = GetTargetName(path.targetInfo);
                var isInternal = IsInternalOutput(path.targetInfo.outputTechnology);

                Console.WriteLine("Path {0}", i);
                Console.WriteLine("  Source: {0}", sourceName);
                Console.WriteLine("  Target: {0}", targetName.monitorFriendlyDeviceName);
                Console.WriteLine("  Device path: {0}", targetName.monitorDevicePath);
                Console.WriteLine("  Output technology: {0} ({1})", path.targetInfo.outputTechnology, OutputTechnologyName(path.targetInfo.outputTechnology));
                Console.WriteLine("  Target available: {0}", path.targetInfo.targetAvailable);
                Console.WriteLine("  Would auto-rotate: {0}", isInternal);
                Console.WriteLine();
            }

            return;
        }
    }

    private static string GetSourceName(DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo)
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

        return result == ERROR_SUCCESS
            ? sourceName.viewGdiDeviceName
            : "(source name unavailable)";
    }

    private static DISPLAYCONFIG_TARGET_DEVICE_NAME GetTargetName(
        DISPLAYCONFIG_PATH_TARGET_INFO targetInfo)
    {
        var targetName = new DISPLAYCONFIG_TARGET_DEVICE_NAME
        {
            monitorFriendlyDeviceName = string.Empty,
            monitorDevicePath = string.Empty
        };
        targetName.header.type = DISPLAYCONFIG_DEVICE_INFO_GET_TARGET_NAME;
        targetName.header.size = (uint)Marshal.SizeOf<DISPLAYCONFIG_TARGET_DEVICE_NAME>();
        targetName.header.adapterId = targetInfo.adapterId;
        targetName.header.id = targetInfo.id;

        var result = DisplayConfigGetDeviceInfo(ref targetName);

        if (result != ERROR_SUCCESS)
        {
            targetName.monitorFriendlyDeviceName = "(target name unavailable)";
            targetName.monitorDevicePath = "(target path unavailable)";
        }

        return targetName;
    }

    private static bool IsInternalOutput(uint outputTechnology)
    {
        return outputTechnology == 6 ||
            outputTechnology == 11 ||
            outputTechnology == 13 ||
            outputTechnology == 0x80000000;
    }

    private static string OutputTechnologyName(uint outputTechnology)
    {
        switch (outputTechnology)
        {
            case 0xFFFFFFFF: return "Other";
            case 0: return "HD15";
            case 1: return "SVideo";
            case 2: return "Composite";
            case 3: return "Component";
            case 4: return "DVI";
            case 5: return "HDMI";
            case 6: return "LVDS";
            case 8: return "D-JPN";
            case 9: return "SDI";
            case 10: return "DisplayPort External";
            case 11: return "DisplayPort Embedded";
            case 12: return "UDI External";
            case 13: return "UDI Embedded";
            case 14: return "SDTV Dongle";
            case 15: return "Miracast";
            case 16: return "Indirect Wired";
            case 17: return "Indirect Virtual";
            case 18: return "DisplayPort USB Tunnel";
            case 0x80000000: return "Internal";
            default: return "Unknown";
        }
    }

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

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern int DisplayConfigGetDeviceInfo(
        ref DISPLAYCONFIG_TARGET_DEVICE_NAME requestPacket);

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
    private struct DISPLAYCONFIG_2DREGION
    {
        public uint cx;
        public uint cy;
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
    private struct POINTL
    {
        public int x;
        public int y;
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

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string viewGdiDeviceName;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DISPLAYCONFIG_TARGET_DEVICE_NAME
    {
        public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
        public uint flags;
        public uint outputTechnology;
        public ushort edidManufactureId;
        public ushort edidProductCodeId;
        public uint connectorInstance;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)]
        public string monitorFriendlyDeviceName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string monitorDevicePath;
    }
}
"@

[DisplayConfigDump]::Dump()
