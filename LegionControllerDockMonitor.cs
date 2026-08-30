using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

internal sealed class LegionControllerDockMonitor : IDisposable
{
    private const ushort LegionVendorId = 0x17EF;
    private static readonly ushort[] LegionGo2ProductIds =
    {
        0x61EB,
        0x61EC,
        0x61ED,
        0x61EE
    };

    private readonly CancellationTokenSource _cts = new();
    private readonly object _stateLock = new();
    private readonly Task _monitorTask;

    private ControllerDockState _currentState = ControllerDockState.Unknown;
    private bool _hasReceivedControllerReport;

    public LegionControllerDockMonitor()
    {
        _monitorTask = Task.Run(() => MonitorAsync(_cts.Token));
    }

    public event EventHandler<ControllerDockStateChangedEventArgs>? DockStateChanged;

    public ControllerDockState CurrentState
    {
        get
        {
            lock (_stateLock)
            {
                return _currentState;
            }
        }
    }

    public bool HasReceivedControllerReport
    {
        get
        {
            lock (_stateLock)
            {
                return _hasReceivedControllerReport;
            }
        }
    }

    private async Task MonitorAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                var devicePath = HidApi.Enumerate()
                    .FirstOrDefault(IsLegionGo2StatusInterface);

                if (devicePath is null)
                {
                    SetState(ControllerDockState.Unknown);
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                    continue;
                }

                await ReadReportsAsync(devicePath, token);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                AppLogger.ThrottledError(
                    "controller-dock-monitor",
                    TimeSpan.FromMinutes(5),
                    "Controller dock monitor failed.",
                    ex);

                SetState(ControllerDockState.Unknown);

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    private static bool IsLegionGo2StatusInterface(string devicePath)
    {
        var lower = devicePath.ToLowerInvariant();

        if (!lower.Contains($"vid_{LegionVendorId:x4}") ||
            !lower.Contains("mi_02"))
        {
            return false;
        }

        return LegionGo2ProductIds.Any(pid => lower.Contains($"pid_{pid:x4}"));
    }

    private async Task ReadReportsAsync(string devicePath, CancellationToken token)
    {
        using var handle = HidApi.CreateHandle(devicePath, out var hasWriteAccess);

        if (handle.IsInvalid)
        {
            SetState(ControllerDockState.Unknown);
            await Task.Delay(TimeSpan.FromSeconds(5), token);
            return;
        }

        AppLogger.Info(
            $"Controller dock monitor opened {devicePath} " +
            $"with {(hasWriteAccess ? "read/write" : "read-only")} access.");

        using var stream = new FileStream(
            handle,
            FileAccess.Read,
            bufferSize: 64,
            isAsync: true);

        while (!token.IsCancellationRequested)
        {
            var buffer = new byte[64];
            int bytesRead;

            try
            {
                using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(token);
                readTimeout.CancelAfter(TimeSpan.FromSeconds(2));

                bytesRead = await stream.ReadAsync(buffer, readTimeout.Token);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            {
                AppLogger.ThrottledError(
                    "controller-dock-report-timeout",
                    TimeSpan.FromMinutes(5),
                    "Controller dock monitor did not receive an input report.");

                break;
            }

            if (bytesRead <= 0)
                break;

            if (!TryParseDockState(buffer, bytesRead, out var state))
                continue;

            SetState(state, hasReceivedControllerReport: true);
        }

        SetState(ControllerDockState.Unknown);
    }

    private static bool TryParseDockState(
        byte[] buffer,
        int bytesRead,
        out ControllerDockState state)
    {
        state = ControllerDockState.Unknown;

        var attachedHeader =
            bytesRead >= 12 &&
            buffer[0] == 0x04 &&
            buffer[1] == 0x00 &&
            buffer[2] == 0xA1;

        var detachedHeader =
            bytesRead >= 14 &&
            buffer[0] == 0x04 &&
            buffer[1] == 0x3C &&
            buffer[2] == 0x74;

        if (!attachedHeader && !detachedHeader)
            return false;

        var connectionOffset = detachedHeader ? 12 : 10;
        var leftCode = buffer[connectionOffset];
        var rightCode = buffer[connectionOffset + 1];

        state = new ControllerDockState(
            DecodeConnection(leftCode),
            DecodeConnection(rightCode));

        return true;
    }

    private static ControllerConnectionState DecodeConnection(byte code)
    {
        return code switch
        {
            0x02 => ControllerConnectionState.Docked,
            0x03 => ControllerConnectionState.Wireless,
            0x01 => ControllerConnectionState.Off,
            _ => ControllerConnectionState.Unknown
        };
    }

    private void SetState(
        ControllerDockState state,
        bool hasReceivedControllerReport = false)
    {
        ControllerDockState previous;
        bool previousHasReceivedControllerReport;

        lock (_stateLock)
        {
            if (_currentState == state &&
                _hasReceivedControllerReport == hasReceivedControllerReport)
            {
                return;
            }

            previous = _currentState;
            previousHasReceivedControllerReport = _hasReceivedControllerReport;
            _currentState = state;
            _hasReceivedControllerReport = hasReceivedControllerReport;
        }

        AppLogger.Info(
            "Controller dock state changed: " +
            $"{previous} (ready={previousHasReceivedControllerReport}) -> " +
            $"{state} (ready={hasReceivedControllerReport}).");

        DockStateChanged?.Invoke(
            this,
            new ControllerDockStateChangedEventArgs(state));
    }

    public void Dispose()
    {
        _cts.Cancel();

        try
        {
            _monitorTask.Wait(TimeSpan.FromSeconds(2));
        }
        catch
        {
        }

        _cts.Dispose();
    }

    private static class HidApi
    {
        private const uint DigcfPresent = 0x00000002;
        private const uint DigcfDeviceInterface = 0x00000010;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;
        private const uint FileFlagOverlapped = 0x40000000;

        public static IEnumerable<string> Enumerate()
        {
            HidD_GetHidGuid(out var hidGuid);

            var deviceInfoSet = SetupDiGetClassDevs(
                ref hidGuid,
                null,
                IntPtr.Zero,
                DigcfPresent | DigcfDeviceInterface);

            if (deviceInfoSet == IntPtr.Zero || deviceInfoSet == new IntPtr(-1))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                for (uint index = 0; ; index++)
                {
                    var interfaceData = new SP_DEVICE_INTERFACE_DATA
                    {
                        cbSize = Marshal.SizeOf<SP_DEVICE_INTERFACE_DATA>()
                    };

                    if (!SetupDiEnumDeviceInterfaces(
                            deviceInfoSet,
                            IntPtr.Zero,
                            ref hidGuid,
                            index,
                            ref interfaceData))
                    {
                        var error = Marshal.GetLastWin32Error();

                        if (error == 259)
                            yield break;

                        throw new Win32Exception(error);
                    }

                    SetupDiGetDeviceInterfaceDetail(
                        deviceInfoSet,
                        ref interfaceData,
                        IntPtr.Zero,
                        0,
                        out var requiredSize,
                        IntPtr.Zero);

                    var detailDataBuffer = Marshal.AllocHGlobal((int)requiredSize);

                    try
                    {
                        Marshal.WriteInt32(
                            detailDataBuffer,
                            IntPtr.Size == 8 ? 8 : 6);

                        if (!SetupDiGetDeviceInterfaceDetail(
                                deviceInfoSet,
                                ref interfaceData,
                                detailDataBuffer,
                                requiredSize,
                                out _,
                                IntPtr.Zero))
                        {
                            throw new Win32Exception(Marshal.GetLastWin32Error());
                        }

                        var devicePath = Marshal.PtrToStringUni(
                            detailDataBuffer + 4);

                        if (!string.IsNullOrWhiteSpace(devicePath))
                            yield return devicePath;
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(detailDataBuffer);
                    }
                }
            }
            finally
            {
                SetupDiDestroyDeviceInfoList(deviceInfoSet);
            }
        }

        public static SafeFileHandle CreateReadHandle(string devicePath)
        {
            return CreateFile(
                devicePath,
                GenericRead,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOverlapped,
                IntPtr.Zero);
        }

        public static SafeFileHandle CreateHandle(
            string devicePath,
            out bool hasWriteAccess)
        {
            var handle = CreateFile(
                devicePath,
                GenericRead | GenericWrite,
                FileShareRead | FileShareWrite,
                IntPtr.Zero,
                OpenExisting,
                FileFlagOverlapped,
                IntPtr.Zero);

            hasWriteAccess = !handle.IsInvalid;

            if (!handle.IsInvalid)
                return handle;

            handle = CreateReadHandle(devicePath);
            hasWriteAccess = false;

            return handle;
        }

        [DllImport("hid.dll")]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr SetupDiGetClassDevs(
            ref Guid classGuid,
            string? enumerator,
            IntPtr hwndParent,
            uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiEnumDeviceInterfaces(
            IntPtr deviceInfoSet,
            IntPtr deviceInfoData,
            ref Guid interfaceClassGuid,
            uint memberIndex,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(
            IntPtr deviceInfoSet,
            ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData,
            IntPtr deviceInterfaceDetailData,
            uint deviceInterfaceDetailDataSize,
            out uint requiredSize,
            IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA
        {
            public int cbSize;
            public Guid InterfaceClassGuid;
            public uint Flags;
            public UIntPtr Reserved;
        }
    }
}

internal sealed record ControllerDockState(
    ControllerConnectionState Left,
    ControllerConnectionState Right)
{
    public static ControllerDockState Unknown { get; } = new(
        ControllerConnectionState.Unknown,
        ControllerConnectionState.Unknown);

    public bool BothDocked =>
        Left == ControllerConnectionState.Docked &&
        Right == ControllerConnectionState.Docked;

    public bool AnyDocked =>
        Left == ControllerConnectionState.Docked ||
        Right == ControllerConnectionState.Docked;
}

internal enum ControllerConnectionState
{
    Unknown,
    Off,
    Docked,
    Wireless
}

internal sealed class ControllerDockStateChangedEventArgs(
    ControllerDockState state) : EventArgs
{
    public ControllerDockState State { get; } = state;
}
