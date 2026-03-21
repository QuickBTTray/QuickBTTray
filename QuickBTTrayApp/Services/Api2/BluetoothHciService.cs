using System.Runtime.InteropServices;
using QuickBTTrayApp.Models;
using QuickBTTrayApp.Services.Contracts;

namespace QuickBTTrayApp.Services.Api2
{
    /// <summary>
    /// HCI API path: disconnect via IOCTL_BTH_DISCONNECT_DEVICE (0x41000c).
    /// Sends a real HCI Disconnect command directly to the Bluetooth radio driver,
    /// which causes the device to appear fully disconnected in Windows Bluetooth Settings.
    ///
    /// Connect uses the same BluetoothSetServiceState approach as the Win32 API path.
    ///
    /// This path is self-contained — all P/Invoke declarations are local to this file
    /// so it can be removed without touching any other code.
    /// </summary>
    public sealed class BluetoothHciService : IBluetoothConnectPath, IBluetoothDisconnectPath
    {
        private const uint IOCTL_BTH_DISCONNECT_DEVICE = 0x41000c;
        private const int  ErrorMoreData               = 234;

        private static readonly Guid HandsfreeServiceGuid = new("0000111e-0000-1000-8000-00805f9b34fb");
        private static readonly Guid AudioSinkServiceGuid  = new("0000110b-0000-1000-8000-00805f9b34fb");

        public BluetoothHciService() { }

        // ── IBluetoothConnectPath ────────────────────────────────────────────
        public Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() => SetServiceStates(deviceName, deviceAddress, enable: true));

        // ── IBluetoothDisconnectPath ─────────────────────────────────────────
        public Task<DeviceToggleResult> DisconnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() => HciDisconnect(deviceName, deviceAddress));

        // ── HCI Disconnect ───────────────────────────────────────────────────
        private static DeviceToggleResult HciDisconnect(string deviceName, string deviceAddress)
        {
            var hFind  = IntPtr.Zero;
            var hRadio = IntPtr.Zero;
            try
            {
                var rfparms = new BLUETOOTH_FIND_RADIO_PARAMS
                    { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>() };
                hFind = BluetoothFindFirstRadio(ref rfparms, out hRadio);

                if (hFind == IntPtr.Zero || hRadio == IntPtr.Zero)
                    return new(deviceName, deviceAddress, ToggleOutcome.Failed, "No Bluetooth radio found.");

                var addr = ParseAddress(deviceAddress);
                var ok   = DeviceIoControl(
                    hRadio, IOCTL_BTH_DISCONNECT_DEVICE,
                    ref addr, sizeof(ulong),
                    IntPtr.Zero, 0, out _, IntPtr.Zero);

                return ok
                    ? new(deviceName, deviceAddress, ToggleOutcome.Disconnected, "Disconnected via HCI IOCTL.")
                    : new(deviceName, deviceAddress, ToggleOutcome.Failed,
                          $"HCI disconnect failed. Win32 error {Marshal.GetLastWin32Error()}.");
            }
            finally
            {
                if (hRadio != IntPtr.Zero) CloseHandle(hRadio);
                if (hFind  != IntPtr.Zero) BluetoothFindRadioClose(hFind);
            }
        }

        // ── API Connect (BluetoothSetServiceState) ────────────────────────────
        private static DeviceToggleResult SetServiceStates(string deviceName, string deviceAddress, bool enable)
        {
            if (!TryFindDeviceInfo(deviceAddress, out var info))
                return new(deviceName, deviceAddress, ToggleOutcome.Failed, "Device not found.");

            var guids = GetServiceGuids(info);
            var ok    = true;
            var flag  = enable ? 1u : 0u;

            foreach (var g in guids)
            {
                var gc = g;
                var r  = BluetoothSetServiceState(IntPtr.Zero, ref info, ref gc, flag);
                if (r == 0) continue;
                if (enable)
                {
                    var dc = g; BluetoothSetServiceState(IntPtr.Zero, ref info, ref dc, 0);
                    var rc = g; if (BluetoothSetServiceState(IntPtr.Zero, ref info, ref rc, 1) != 0) ok = false;
                }
                else { ok = false; }
            }

            var outcome = ok ? (enable ? ToggleOutcome.Connected : ToggleOutcome.Disconnected) : ToggleOutcome.Failed;
            var msg     = ok ? (enable ? "Audio services enabled." : "Audio services disabled.") : "Service-state change failed.";
            return new(deviceName, deviceAddress, outcome, msg);
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static bool TryFindDeviceInfo(string address, out BLUETOOTH_DEVICE_INFO match)
        {
            foreach (var info in EnumerateDevices())
            {
                if (string.Equals(FormatAddress(info.Address), address, StringComparison.OrdinalIgnoreCase))
                { match = info; return true; }
            }
            match = default;
            return false;
        }

        private static List<Guid> GetServiceGuids(BLUETOOTH_DEVICE_INFO info)
        {
            var installed = GetInstalledServices(info);
            var guids     = new List<Guid>();
            if (installed.Contains(HandsfreeServiceGuid)) guids.Add(HandsfreeServiceGuid);
            if (installed.Contains(AudioSinkServiceGuid)) guids.Add(AudioSinkServiceGuid);
            if (guids.Count == 0) { guids.Add(HandsfreeServiceGuid); guids.Add(AudioSinkServiceGuid); }
            return guids;
        }

        private static IReadOnlyCollection<Guid> GetInstalledServices(BLUETOOTH_DEVICE_INFO info)
        {
            var count = 16u;
            var guids = new Guid[count];
            var r     = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref info, ref count, guids);
            if (r == ErrorMoreData && count > 0)
            {
                guids = new Guid[count];
                BluetoothEnumerateInstalledServices(IntPtr.Zero, ref info, ref count, guids);
            }
            return count == 0 ? [] : guids.Take((int)count).ToArray();
        }

        private static IEnumerable<BLUETOOTH_DEVICE_INFO> EnumerateDevices()
        {
            var sp = new BLUETOOTH_DEVICE_SEARCH_PARAMS
            {
                dwSize               = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                fReturnAuthenticated = 1,
                fReturnRemembered    = 1,
                fReturnConnected     = 1,
                fReturnUnknown       = 0,
                fIssueInquiry        = 0,
                cTimeoutMultiplier   = 0,
                hRadio               = IntPtr.Zero
            };
            var di = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };
            var h  = BluetoothFindFirstDevice(ref sp, ref di);
            if (h == IntPtr.Zero) yield break;
            try
            {
                do { yield return di; di.dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>(); }
                while (BluetoothFindNextDevice(h, ref di));
            }
            finally { BluetoothFindDeviceClose(h); }
        }

        // Address "AA:BB:CC:DD:EE:FF" → ulong stored little-endian in memory
        private static ulong ParseAddress(string address)
        {
            var bytes  = address.Split(':').Select(h => Convert.ToByte(h, 16)).ToArray();
            var padded = new byte[8];
            for (int i = 0; i < 6; i++) padded[i] = bytes[5 - i];
            return BitConverter.ToUInt64(padded, 0);
        }

        private static string FormatAddress(ulong addr)
            => string.Join(":", BitConverter.GetBytes(addr).Take(6).Reverse().Select(b => b.ToString("X2")));

        // ── P/Invoke ─────────────────────────────────────────────────────────
        [DllImport("Bthprops.cpl", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS p, out IntPtr phRadio);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BluetoothFindRadioClose(IntPtr hFind);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        private static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS p, ref BLUETOOTH_DEVICE_INFO d);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BluetoothFindNextDevice(IntPtr h, ref BLUETOOTH_DEVICE_INFO d);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool BluetoothFindDeviceClose(IntPtr h);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        private static extern int BluetoothEnumerateInstalledServices(IntPtr radio, ref BLUETOOTH_DEVICE_INFO d, ref uint count, [Out] Guid[]? guids);

        [DllImport("Bthprops.cpl", SetLastError = true)]
        private static extern int BluetoothSetServiceState(IntPtr radio, ref BLUETOOTH_DEVICE_INFO d, ref Guid guid, uint flags);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeviceIoControl(
            IntPtr hDevice, uint ioControlCode,
            ref ulong inBuffer, int inBufferSize,
            IntPtr outBuffer, int outBufferSize,
            out int bytesReturned, IntPtr overlapped);

        [DllImport("kernel32", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        // ── Native structs ───────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_FIND_RADIO_PARAMS { public uint dwSize; }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint   dwSize;
            public int    fReturnAuthenticated;
            public int    fReturnRemembered;
            public int    fReturnUnknown;
            public int    fReturnConnected;
            public int    fIssueInquiry;
            public byte   cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public uint   dwSize;
            public ulong  Address;
            public uint   ulClassofDevice;
            public int    fConnected;
            public int    fRemembered;
            public int    fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }
    }
}
