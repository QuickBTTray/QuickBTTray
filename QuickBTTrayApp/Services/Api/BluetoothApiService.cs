using System.Diagnostics;
using System.Runtime.InteropServices;
using QuickBTTrayApp.Models;
using QuickBTTrayApp.Services.Contracts;

namespace QuickBTTrayApp.Services.Api
{
    /// <summary>
    /// WIN32 API path: device discovery + connect + disconnect via BluetoothSetServiceState.
    /// This is the API path - can be removed independently of the UI (UIA) path.
    /// </summary>
    public sealed class BluetoothApiService : IBluetoothDeviceDiscovery, IBluetoothConnectPath, IBluetoothDisconnectPath
    {
        private const int ErrorMoreData = 234;
        private const int ErrorNotFound = 1168;
        private static readonly Guid HandsfreeServiceGuid = new("0000111e-0000-1000-8000-00805f9b34fb");
        private static readonly Guid AudioSinkServiceGuid = new("0000110b-0000-1000-8000-00805f9b34fb");
        private static readonly TimeSpan InstalledServicesCacheTtl = TimeSpan.FromSeconds(45);

        private readonly Dictionary<string, CachedInstalledServices> _installedServicesCache =
            new(StringComparer.OrdinalIgnoreCase);
        private readonly object _cacheGate = new();

        public BluetoothApiService() { }

        private sealed record CachedInstalledServices(DateTimeOffset ExpiresAt, IReadOnlyCollection<Guid> Services);

        // -- IBluetoothDeviceDiscovery ------------------------------------------------
        public IReadOnlyList<BluetoothAudioDevice> GetAudioDevices()
        {
            var devices = new List<BluetoothAudioDevice>();
            foreach (var info in EnumerateDevices())
            {
                if (string.IsNullOrWhiteSpace(info.szName)) continue;

                var address = FormatAddress(info.Address);
                var services = GetInstalledServicesCached(address, info);
                var supportsHfp = services.Contains(HandsfreeServiceGuid);
                var supportsA2dp = services.Contains(AudioSinkServiceGuid);
                var isAudio = supportsHfp || supportsA2dp || GetMajorDeviceClass(info.ulClassofDevice) == 0x04;
                if (!isAudio) continue;

                devices.Add(new BluetoothAudioDevice
                {
                    Name = info.szName.Trim(),
                    Address = address,
                    IsConnected = info.fConnected != 0,
                    SupportsHandsfree = supportsHfp,
                    SupportsAudioSink = supportsA2dp
                });
            }

            return devices
                .OrderBy(d => d.Name, StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(d => d.Address)
                .ToList();
        }

        // -- IBluetoothConnectPath ----------------------------------------------------
        public Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var results = SetServiceStates([deviceAddress], enable: true);
                sw.Stop();
                var outcome = results.Count > 0 ? results[0].Outcome.ToString() : "NoResult";
                Debug.WriteLine($"[API-CONNECT] {deviceAddress} finished in {sw.ElapsedMilliseconds} ms, outcome={outcome}");

                return results.Count > 0
                    ? results[0]
                    : new DeviceToggleResult(deviceName, deviceAddress, ToggleOutcome.Failed, "No result.");
            });

        // -- IBluetoothDisconnectPath -------------------------------------------------
        public Task<DeviceToggleResult> DisconnectAsync(string deviceName, string deviceAddress)
            => Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();
                var results = SetServiceStates([deviceAddress], enable: false);
                sw.Stop();
                var outcome = results.Count > 0 ? results[0].Outcome.ToString() : "NoResult";
                Debug.WriteLine($"[API-DISCONNECT] {deviceAddress} finished in {sw.ElapsedMilliseconds} ms, outcome={outcome}");

                return results.Count > 0
                    ? results[0]
                    : new DeviceToggleResult(deviceName, deviceAddress, ToggleOutcome.Failed, "No result.");
            });

        // -- Internal -----------------------------------------------------------------
        private IReadOnlyList<DeviceToggleResult> SetServiceStates(IEnumerable<string> addresses, bool enable)
        {
            var targetAddresses = addresses
                .Where(a => !string.IsNullOrWhiteSpace(a))
                .Select(a => a.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var byAddr = BuildDeviceInfoMap(targetAddresses);
            var results = new List<DeviceToggleResult>();

            Debug.WriteLine($"[API-STATE] enable={enable}, targets={targetAddresses.Count}, found={byAddr.Count}");

            foreach (var address in targetAddresses)
            {
                if (!byAddr.TryGetValue(address, out var info))
                {
                    Debug.WriteLine($"[API-STATE] address-not-found: {address}");
                    results.Add(new(address, address, ToggleOutcome.Failed, "Device not found."));
                    continue;
                }

                var deviceName = string.IsNullOrWhiteSpace(info.szName) ? address : info.szName.Trim();
                var guids = GetServiceGuids(address, info);
                var flag = enable ? 1u : 0u;

                Debug.WriteLine($"[API-STATE] address={address}, servicesToToggle={guids.Count}, enable={enable}");

                // Toggle all GUIDs in parallel — each task captures its own struct copy (value type).
                var guidSw = Stopwatch.StartNew();
                var guidTasks = guids.Select(g => Task.Run(() =>
                {
                    var localInfo = info; // struct copy — safe to use independently on another thread
                    var gc = g;
                    var r = BluetoothSetServiceState(IntPtr.Zero, ref localInfo, ref gc, flag);
                    if (r == 0) return true;

                    Debug.WriteLine($"[API-STATE] first-call failed, address={address}, guid={g}, enable={enable}, win32={r}");

                    if (enable)
                    {
                        var li2 = info;
                        var dc = g;
                        var dr = BluetoothSetServiceState(IntPtr.Zero, ref li2, ref dc, 0);
                        var li3 = info;
                        var rc = g;
                        var rr = BluetoothSetServiceState(IntPtr.Zero, ref li3, ref rc, 1);

                        if (rr != 0)
                        {
                            Debug.WriteLine($"[API-STATE] retry failed, address={address}, guid={g}, disableResult={dr}, reEnableResult={rr}, win32={rr}");
                            return false;
                        }
                        Debug.WriteLine($"[API-STATE] retry succeeded, address={address}, guid={g}, disableResult={dr}");
                        return true;
                    }

                    // Disconnect should be idempotent: "not found" means the service is already disabled.
                    if (r == ErrorNotFound)
                    {
                        Debug.WriteLine($"[API-STATE] already-disabled, address={address}, guid={g}, win32={r}");
                        return true;
                    }

                    return false;
                })).ToList();

                Task.WhenAll(guidTasks).GetAwaiter().GetResult();
                Debug.WriteLine($"[API-STATE] parallel-guids finished in {guidSw.ElapsedMilliseconds} ms, address={address}");

                var ok = guidTasks.All(t => t.Result);

                if (!ok)
                {
                    InvalidateInstalledServicesCache(address);
                }

                if (!enable && ok && TryGetCurrentConnectionState(address, out var stillConnected) && stillConnected)
                {
                    ok = false;
                }

                var outcome = ok
                    ? (enable ? ToggleOutcome.Connected : ToggleOutcome.Disconnected)
                    : ToggleOutcome.Failed;
                var msg = ok
                    ? (enable ? "Audio services enabled." : "Audio services disabled.")
                    : (!enable
                        ? "Audio services may be disabled, but device is still connected in Windows. Use UI or HCI disconnect for full link drop."
                        : "Service-state change failed.");

                Debug.WriteLine($"[API-STATE] result address={address}, outcome={outcome}, msg={msg}");

                results.Add(new(deviceName, address, outcome, msg));
            }

            return results;
        }

        private static Dictionary<string, BLUETOOTH_DEVICE_INFO> BuildDeviceInfoMap(IReadOnlyCollection<string> addresses)
        {
            var wanted = addresses.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var map = new Dictionary<string, BLUETOOTH_DEVICE_INFO>(StringComparer.OrdinalIgnoreCase);

            foreach (var info in EnumerateDevices())
            {
                var addr = FormatAddress(info.Address);
                if (!wanted.Contains(addr)) continue;
                map[addr] = info;
                if (map.Count == wanted.Count) break;
            }

            return map;
        }

        private static bool TryGetCurrentConnectionState(string address, out bool isConnected)
        {
            foreach (var info in EnumerateDevices())
            {
                var current = FormatAddress(info.Address);
                if (!string.Equals(current, address, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                isConnected = info.fConnected != 0;
                return true;
            }

            isConnected = false;
            return false;
        }

        private List<Guid> GetServiceGuids(string address, BLUETOOTH_DEVICE_INFO info)
        {
            var services = GetInstalledServicesCached(address, info);
            var guids = new List<Guid>();

            // A2DP first for output-device-first connect behavior.
            if (services.Contains(AudioSinkServiceGuid)) guids.Add(AudioSinkServiceGuid);
            if (services.Contains(HandsfreeServiceGuid)) guids.Add(HandsfreeServiceGuid);

            // Fallback: try both (matches original AHK script behavior).
            if (guids.Count == 0)
            {
                guids.Add(AudioSinkServiceGuid);
                guids.Add(HandsfreeServiceGuid);
            }

            return guids;
        }

        private IReadOnlyCollection<Guid> GetInstalledServicesCached(string address, BLUETOOTH_DEVICE_INFO info)
        {
            var key = address.Trim();
            var now = DateTimeOffset.UtcNow;

            lock (_cacheGate)
            {
                if (_installedServicesCache.TryGetValue(key, out var cached) && cached.ExpiresAt > now)
                {
                    return cached.Services;
                }
            }

            var services = GetInstalledServicesNative(info);

            lock (_cacheGate)
            {
                _installedServicesCache[key] = new CachedInstalledServices(now.Add(InstalledServicesCacheTtl), services);
            }

            return services;
        }

        private void InvalidateInstalledServicesCache(string address)
        {
            lock (_cacheGate)
            {
                _installedServicesCache.Remove(address.Trim());
            }
        }

        private static IReadOnlyCollection<Guid> GetInstalledServicesNative(BLUETOOTH_DEVICE_INFO info)
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
                dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                fReturnAuthenticated = 1,
                fReturnRemembered = 1,
                fReturnConnected = 1,
                fReturnUnknown = 0,
                fIssueInquiry = 0,
                cTimeoutMultiplier = 0,
                hRadio = IntPtr.Zero
            };

            var di = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };
            var h = BluetoothFindFirstDevice(ref sp, ref di);
            if (h == IntPtr.Zero) yield break;

            try
            {
                do
                {
                    yield return di;
                    di.dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>();
                }
                while (BluetoothFindNextDevice(h, ref di));
            }
            finally
            {
                BluetoothFindDeviceClose(h);
            }
        }

        private static uint GetMajorDeviceClass(uint cod) => (cod & 0x1F00) >> 8;

        private static string FormatAddress(ulong addr)
            => string.Join(":", BitConverter.GetBytes(addr).Take(6).Reverse().Select(b => b.ToString("X2")));

        // -- P/Invoke -----------------------------------------------------------------
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

        // -- Native structs ------------------------------------------------------------
        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            public int fReturnAuthenticated;
            public int fReturnRemembered;
            public int fReturnUnknown;
            public int fReturnConnected;
            public int fIssueInquiry;
            public byte cTimeoutMultiplier;
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
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            public int fConnected;
            public int fRemembered;
            public int fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }
    }
}
