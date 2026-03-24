using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;
using QuickBTTrayApp.Services.Contracts;

namespace QuickBTTrayApp.Services.Ui
{
    /// <summary>
    /// UI Automation path: connect/disconnect via Windows Settings Bluetooth page.
    /// This is the UI path — can be removed independently of the API path.
    /// </summary>
    public sealed class BluetoothUiaService : IBluetoothConnectPath, IBluetoothDisconnectPath
    {
        private const int SettingsWindowTimeoutMs = 8000;
        private const int ButtonReadyTimeoutMs = 6000;
        private const int InitialSettleDelayMs = 200;
        private const int ReadyPollIntervalMs = 200;
        private const int PostClickConfirmTimeoutMs = 900;
        private const int PostClickConfirmPollMs = 150;
        private const int CloseAfterConfirmDelayMs = 180;
        private const int CloseFallbackDelayMs = 120;

        [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
        private const int  SW_RESTORE        = 9;
        private const byte VK_END            = 0x23;
        private const uint KEYEVENTF_KEYUP   = 0x0002;

           public BluetoothUiaService() { }

        public async Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress)
        {
            var ok = await InvokeDeviceActionAsync(deviceName, "Connect");
            return ok
                ? new(deviceName, deviceAddress, ToggleOutcome.Connected, "Connected via Settings UI.")
                : new(deviceName, deviceAddress, ToggleOutcome.Failed, "Settings UI connect failed.");
        }

        public async Task<DeviceToggleResult> DisconnectAsync(string deviceName, string deviceAddress)
        {
            var ok = await InvokeDeviceActionAsync(deviceName, "Disconnect");
            return ok
                ? new(deviceName, deviceAddress, ToggleOutcome.Disconnected, "Disconnected via Settings UI.")
                : new(deviceName, deviceAddress, ToggleOutcome.Failed, "Settings UI disconnect failed.");
        }

        private async Task<bool> InvokeDeviceActionAsync(string deviceName, string action)
        {
            try
            {
                bool hadSettingsWindow = FindSettingsWindow() != null;

                // Always invoke the Bluetooth settings URI so the Settings app navigates
                // to the expected page, even if a Settings window already exists.
                Process.Start(new ProcessStartInfo { FileName = "ms-settings:bluetooth", UseShellExecute = true });

                var win = await WaitForSettingsWindowAsync(SettingsWindowTimeoutMs);
                if (win == null) return false;

                await PrimeBluetoothPageAsync(win);

                var btn = await WaitForClickableButtonAsync(win, deviceName, action, ButtonReadyTimeoutMs);
                if (btn == null && hadSettingsWindow)
                {
                    TryCloseSettingsWindow(win);
                    await Task.Delay(250);

                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:bluetooth", UseShellExecute = true });
                    win = await WaitForSettingsWindowAsync(SettingsWindowTimeoutMs);
                    if (win != null)
                    {
                        await PrimeBluetoothPageAsync(win);
                        btn = await WaitForClickableButtonAsync(win, deviceName, action, ButtonReadyTimeoutMs);
                    }
                }
                if (btn == null)
                {
                    return false;
                }
                if (win == null)
                {
                    return false;
                }

                TryInvokeElement(btn);

                var expectedNextAction = action.Equals("Connect", StringComparison.OrdinalIgnoreCase)
                    ? "Disconnect"
                    : "Connect";
                var clickConfirmed = await WaitForStateTransitionAsync(
                    win!,
                    deviceName,
                    previousAction: action,
                    expectedNextAction: expectedNextAction,
                    timeoutMs: PostClickConfirmTimeoutMs);

                if (!hadSettingsWindow)
                {
                    try
                    {
                        await Task.Delay(clickConfirmed ? CloseAfterConfirmDelayMs : CloseFallbackDelayMs);
                        win!.SetFocus();
                        if (win.GetCurrentPattern(WindowPattern.Pattern) is WindowPattern cp) cp.Close();
                    }
                       catch { }
                }
                else
                {
                    TryMoveFocusOffActionButton(win);
                }
                return true;
            }
               catch { return false; }
        }

        private async Task<AutomationElement?> WaitForClickableButtonAsync(
            AutomationElement win,
            string deviceName,
            string action,
            int timeoutMs)
        {
            if (timeoutMs >= ButtonReadyTimeoutMs)
            {
                // Give Settings a brief chance to finish first render after navigation.
                await Task.Delay(InitialSettleDelayMs);
            }

            // Cache win reference for End-key nudges inside the loop.
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var deviceEl = FindElementByName(win, deviceName);
                if (deviceEl != null)
                {
                    TryScrollIntoView(deviceEl);
                    var btn = SearchForButton(win, deviceEl, action);
                    if (IsElementClickable(btn))
                    {
                        return btn;
                    }
                }
                else
                {
                    // Device not in UIA tree yet — try to pull Audio section into view first.
                    if (!TryBringSectionIntoView(win, "Audio"))
                    {
                        // Fallback: nudge full content rehydration.
                        var anchor = FindButtonNamed(win, "Add device") ?? FindButtonNamed(win, "Devices");
                        if (!TryRehydrateDeviceSections(anchor, win))
                        {
                            TrySendEndKey(win);
                        }
                    }
                }

                await Task.Delay(timeoutMs >= ButtonReadyTimeoutMs ? ReadyPollIntervalMs : PostClickConfirmPollMs);
            }

            return null;
        }

        private static bool IsElementClickable(AutomationElement? el)
        {
            if (el == null) return false;
            try
            {
                if (!el.Current.IsEnabled) return false;
                if (el.Current.IsOffscreen) return false;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> WaitForStateTransitionAsync(
            AutomationElement win,
            string deviceName,
            string previousAction,
            string expectedNextAction,
            int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var deviceEl = FindElementByName(win, deviceName);
                if (deviceEl != null)
                {
                    TryScrollIntoView(deviceEl);
                    var nextBtn = SearchForButton(win, deviceEl, expectedNextAction);
                    if (nextBtn != null)
                    {
                        return true;
                    }

                    var prevBtn = SearchForButton(win, deviceEl, previousAction);
                    if (prevBtn == null || !IsElementClickable(prevBtn))
                    {
                        return true;
                    }
                }

                await Task.Delay(PostClickConfirmPollMs);
            }

            return false;
        }

        private async Task<AutomationElement?> WaitForSettingsWindowAsync(int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var w = FindSettingsWindow();
                if (w != null) return w;
                await Task.Delay(300);
            }
            return null;
        }

        private static AutomationElement? FindSettingsWindow()
        {
            var topLevelWindows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            foreach (AutomationElement w in topLevelWindows)
            {
                var name = w.Current.Name ?? string.Empty;
                var className = w.Current.ClassName ?? string.Empty;
                if (!name.Contains("Settings", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (className.Equals("ApplicationFrameWindow", StringComparison.OrdinalIgnoreCase)
                    || className.Equals("WinUIDesktopWin32WindowClass", StringComparison.OrdinalIgnoreCase)
                    || string.IsNullOrWhiteSpace(className))
                {
                    return w;
                }
            }

            return null;
        }

        private async Task PrimeBluetoothPageAsync(AutomationElement win)
        {
            var hwnd = new IntPtr(win.Current.NativeWindowHandle);
            ShowWindow(hwnd, SW_RESTORE);

            var pageAnchor = await WaitForPageLandmarkAsync(win, ButtonReadyTimeoutMs);

            bool primedDevices = TryInvokeButtonByName(win, "Devices");
            if (primedDevices)
            {
                await Task.Delay(InitialSettleDelayMs);
            }

            if (!TryRehydrateDeviceSections(pageAnchor, win))
            {
                TrySendEndKey(win);
            }

            await Task.Delay(InitialSettleDelayMs);
        }

        private static void TryCloseSettingsWindow(AutomationElement win)
        {
            try
            {
                if (win.GetCurrentPattern(WindowPattern.Pattern) is WindowPattern wp)
                {
                    wp.Close();
                }
            }
            catch { }
        }

        private AutomationElement? FindElementByName(AutomationElement root, string name)
        {
            try
            {
                return root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase));
            }
            catch { return null; }
        }

        private AutomationElement? SearchForButton(AutomationElement win, AutomationElement deviceEl, string btnName)
        {
            var current = deviceEl;
            for (int i = 0; i < 12; i++)
            {
                var btn = FindButtonNamed(current, btnName);
                if (btn != null) return btn;
                try
                {
                    var parent = TreeWalker.RawViewWalker.GetParent(current);
                    if (parent == null || parent == AutomationElement.RootElement) break;
                    current = parent;
                }
                catch { break; }
            }
            return null;
        }

        private async Task<AutomationElement?> WaitForPageLandmarkAsync(AutomationElement win, int timeoutMs)
        {
            // "Add device" appears early in the Bluetooth page render, well before the
            // device list sections load. Waiting for it ensures the page is live and will
            // respond to keyboard input before we send the End key.
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = FindButtonNamed(win, "Add device");
                if (el != null) return el;
                await Task.Delay(ReadyPollIntervalMs);
            }

            return null;
        }

        private static bool TryRehydrateDeviceSections(AutomationElement? anchor, AutomationElement win)
        {
            try
            {
                var focusTarget = anchor ?? FindButtonNamed(win, "Devices") ?? win;
                focusTarget.SetFocus();

                var scroller = FindBestContentScroller(win) ?? FindScrollableAncestor(focusTarget);
                if (scroller == null) return false;
                if (!scroller.TryGetCurrentPattern(ScrollPattern.Pattern, out var patternObj)) return false;

                var scroll = (ScrollPattern)patternObj;
                if (!scroll.Current.VerticallyScrollable) return false;

                // Top -> Bottom cycle nudges WinUI settings into materializing virtualized
                // device rows that are sometimes missing when window is already open.
                scroll.SetScrollPercent(ScrollPattern.NoScroll, 0.0);
                scroll.SetScrollPercent(ScrollPattern.NoScroll, 100.0);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static bool TryBringSectionIntoView(AutomationElement win, string sectionName)
        {
            try
            {
                var section = FindElementByNameStatic(win, sectionName);
                if (section == null) return false;
                if (!section.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var patternObj)) return false;
                ((ScrollItemPattern)patternObj).ScrollIntoView();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static AutomationElement? FindBestContentScroller(AutomationElement win)
        {
            try
            {
                var all = win.FindAll(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.IsScrollPatternAvailableProperty, true));

                AutomationElement? best = null;
                double bestArea = -1;
                foreach (AutomationElement el in all)
                {
                    if (!el.TryGetCurrentPattern(ScrollPattern.Pattern, out var patternObj)) continue;
                    var scroll = (ScrollPattern)patternObj;
                    if (!scroll.Current.VerticallyScrollable) continue;

                    var r = el.Current.BoundingRectangle;
                    if (double.IsInfinity(r.Width) || double.IsInfinity(r.Height)) continue;
                    if (r.Width <= 0 || r.Height <= 0) continue;

                    double area = r.Width * r.Height;
                    if (area > bestArea)
                    {
                        bestArea = area;
                        best = el;
                    }
                }

                return best;
            }
            catch
            {
                return null;
            }
        }

        private static AutomationElement? FindScrollableAncestor(AutomationElement start)
        {
            var current = start;
            for (int i = 0; i < 12; i++)
            {
                try
                {
                    if (current.TryGetCurrentPattern(ScrollPattern.Pattern, out var patternObj))
                    {
                        var scroll = (ScrollPattern)patternObj;
                        if (scroll.Current.VerticallyScrollable)
                        {
                            return current;
                        }
                    }

                    var parent = TreeWalker.RawViewWalker.GetParent(current);
                    if (parent == null || parent == AutomationElement.RootElement)
                    {
                        break;
                    }
                    current = parent;
                }
                catch
                {
                    break;
                }
            }

            return null;
        }

        private static void TrySendEndKey(AutomationElement win)
        {
            try
            {
                var focusTarget = FindButtonNamed(win, "Add device") ?? FindButtonNamed(win, "Devices") ?? win;
                focusTarget.SetFocus();
                keybd_event(VK_END, 0, 0,              UIntPtr.Zero); // key down
                keybd_event(VK_END, 0, KEYEVENTF_KEYUP, UIntPtr.Zero); // key up
            }
            catch { }
        }

        private static void TryMoveFocusOffActionButton(AutomationElement win)
        {
            try
            {
                // Prefer a neutral control so the clicked device action button no longer
                // looks focused when Settings remains open.
                var focusTarget = FindElementByNameStatic(win, "Search box, Find a setting")
                    ?? FindButtonNamed(win, "Expand search box")
                    ?? FindButtonNamed(win, "Back")
                    ?? win;
                focusTarget.SetFocus();
            }
            catch { }
        }

        private static bool TryInvokeButtonByName(AutomationElement root, string name)
        {
            try
            {
                var btn = FindButtonNamed(root, name);
                if (btn == null) return false;
                TryInvokeElement(btn);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static AutomationElement? FindElementByNameStatic(AutomationElement root, string name)
        {
            try
            {
                return root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase));
            }
            catch
            {
                return null;
            }
        }

        private static void TryScrollIntoView(AutomationElement element)
        {
            try
            {
                if (element.TryGetCurrentPattern(ScrollItemPattern.Pattern, out var pattern))
                    ((ScrollItemPattern)pattern).ScrollIntoView();
            }
            catch { }
        }

        private static AutomationElement? FindButtonNamed(AutomationElement root, string name)
        {
            try
            {
                return root.FindFirst(TreeScope.Descendants, new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty,       name,               PropertyConditionFlags.IgnoreCase),
                    new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button)));
            }
            catch { return null; }
        }

        private static void TryInvokeElement(AutomationElement el)
        {
            if (el.TryGetCurrentPattern(InvokePattern.Pattern, out var inv))
                { ((InvokePattern)inv).Invoke(); return; }
            if (el.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var exp))
                ((ExpandCollapsePattern)exp).Expand();
        }
    }
}
