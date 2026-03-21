using System.Runtime.InteropServices;
using QuickBTTrayApp.Models;
using QuickBTTrayApp.Services.Contracts;

namespace QuickBTTrayApp.Services.Api
{
    /// <summary>
    /// WIN32 API path: device discovery + connect + disconnect via BluetoothSetServiceState.
    /// This is the API path — can be removed independently of the UI (UIA) path.
    /// </summary>
    public sealed class BluetoothApiService : IBluetoothDeviceDiscovery, IBluetoothConnectPath, IBluetoothDisconnectPath
    {
        private const int ErrorMoreData = 234;
        private static readonly Guid HandsfreeServiceGuid = new("0000111e-0000-1000-8000-00805f9b34fb");
        private static readonly Guid AudioSinkServiceGuid  = new("0000110b-0000-1000-8000-00805f9b34fb");

           public BluetoothApiService() { }

        // ── IBluetoothDeviceDiscovery ────────────────────────────────────────
        public IReadOnlyList<BluetoothAudioDevice> GetAudioDevices()
        {
            var devices = new List<BluetoothAudioDevice>();
            foreach (var info in EnumerateDevices())
            {
                if (string.IsNullOrWhiteSpace(info.szName)) continue;
                var services      = GetInstalledServices(info);
                var supportsHfp   = services.Contains(HandsfreeServiceGuid);
                var supportsA2dp  = services.Contains(AudioSinkServiceGuid);
                var isAudio       = supportsHfp || supportsA2dp || GetMajorDeviceClass(info.ulClassofDevice) == 0x04;
                if (!isAudio) continue;
                devices.Add(new BluetoothAudioDevice
                {
                    Name              = info.szName.Trim(),
                    Address           = FormatAddress(info.Address),
                    IsConnected       = info.fConnected != 0,
                    SupportsHandsfree = supportsHfp,
                    SupportsAudioSink = supportsA2dp
                });
            }
            var sorted = devices
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.Address)
                .ToList();
            return sorted;
        }

        // ── IBluetoothConnectPath ────────────────────────────────────────────
        public Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() =>
            {
                var results = SetServiceStates([deviceAddress], enable: true);
                return results.Count > 0 ? results[0]
                    : new DeviceToggleResult(deviceName, deviceAddress, ToggleOutcome.Failed, "No result.");
            });

        // ── IBluetoothDisconnectPath ─────────────────────────────────────────
        public Task<DeviceToggleResult> DisconnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() =>
            {
                var results = SetServiceStates([deviceAddress], enable: false);
                return results.Count > 0 ? results[0]
                    : new DeviceToggleResult(deviceName, deviceAddress, ToggleOutcome.Failed, "No result.");
            });

        // ── Internal ─────────────────────────────────────────────────────────
        private IReadOnlyList<DeviceToggleResult> SetServiceStates(IEnumerable<string> addresses, bool enable)
        {
            var byAddr  = GetAudioDevices().ToDictionary(d => d.Address, StringComparer.OrdinalIgnoreCase);
            var results = new List<DeviceToggleResult>();

            foreach (var address in addresses)
            {
                if (!byAddr.TryGetValue(address, out var device))
                {
                    results.Add(new(address, address, ToggleOutcome.Failed, "Device not found."));
                    continue;
                }
                if (!TryFindDeviceInfo(address, out var info))
                {
                    results.Add(new(device.Name, device.Address, ToggleOutcome.Failed, "Device info lookup failed."));
                    continue;
                }

                var guids = GetServiceGuids(device);
                var ok    = true;
                var flag  = enable ? 1u : 0u;

                foreach (var g in guids)
                {
                    var gc = g;
                    var r  = BluetoothSetServiceState(IntPtr.Zero, ref info, ref gc, flag);
                    if (r == 0) continue;

                    var err = Marshal.GetLastWin32Error();

                       if (enable)
                    {
                        // Retry: disable first, then re-enable (matches WinForms reference logic)
                        var dc = g; BluetoothSetServiceState(IntPtr.Zero, ref info, ref dc, 0);
                        var rc = g; if (BluetoothSetServiceState(IntPtr.Zero, ref info, ref rc, 1) != 0) ok = false;
                    }
                    else { ok = false; }
                }

                var outcome = ok
                    ? (enable ? ToggleOutcome.Connected : ToggleOutcome.Disconnected)
                    : ToggleOutcome.Failed;
                var msg = ok
                    ? (enable ? "Audio services enabled." : "Audio services disabled.")
                    : "Service-state change failed.";
                results.Add(new(device.Name, device.Address, outcome, msg));
            }
            return results;
        }

        private static List<Guid> GetServiceGuids(BluetoothAudioDevice d)
        {
            var guids = new List<Guid>();
            if (d.SupportsHandsfree) guids.Add(HandsfreeServiceGuid);
            if (d.SupportsAudioSink) guids.Add(AudioSinkServiceGuid);
            // Fallback: try both (matches original AHK script behavior)
            if (guids.Count == 0) { guids.Add(HandsfreeServiceGuid); guids.Add(AudioSinkServiceGuid); }
            return guids;
        }

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

        private static IReadOnlyCollection<Guid> GetInstalledServices(BLUETOOTH_DEVICE_INFO info)
        {
            var count = 16u;
            var guids = new Guid[count];
            var r = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref info, ref count, guids);
            if (r == ErrorMoreData && count > 0)
            {
                guids = new Guid[count];
                r = BluetoothEnumerateInstalledServices(IntPtr.Zero, ref info, ref count, guids);
            }
            return (r != 0 || count == 0) ? [] : guids.Take((int)count).ToArray();
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

        private static uint   GetMajorDeviceClass(uint cod) => (cod & 0x1F00) >> 8;
        private static string FormatAddress(ulong addr)
            => string.Join(":", BitConverter.GetBytes(addr).Take(6).Reverse().Select(b => b.ToString("X2")));

        // ── P/Invoke ─────────────────────────────────────────────────────────
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

        // ── Native structs ───────────────────────────────────────────────────
        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint    dwSize;
            public int     fReturnAuthenticated;
            public int     fReturnRemembered;
            public int     fReturnUnknown;
            public int     fReturnConnected;
            public int     fIssueInquiry;
            public byte    cTimeoutMultiplier;
            public IntPtr  hRadio;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear, wMonth, wDayOfWeek, wDay, wHour, wMinute, wSecond, wMilliseconds;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public uint    dwSize;
            public ulong   Address;
            public uint    ulClassofDevice;
            public int     fConnected;
            public int     fRemembered;
            public int     fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string  szName;
        }
    }
}
