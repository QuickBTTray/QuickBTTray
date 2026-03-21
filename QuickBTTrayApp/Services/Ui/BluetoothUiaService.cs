using System.Diagnostics;
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
           public BluetoothUiaService() { }

        public async Task<DeviceToggleResult> ConnectAsync(string deviceName, string deviceAddress)
        {
            var ok = await InvokeDeviceActionAsync(deviceName, "Connect");
            return ok
                ? new(deviceName, deviceAddress, ToggleOutcome.Connected,    "Connected via Settings UI.")
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
                bool openedByApp = FindSettingsWindow() == null;
                if (openedByApp)
                    Process.Start(new ProcessStartInfo { FileName = "ms-settings:bluetooth", UseShellExecute = true });

                var win = await WaitForSettingsWindowAsync(8000);
                   if (win == null) return false;

                await Task.Delay(1500);

                var deviceEl = await FindElementByNameAsync(win, deviceName, 6000);
                   if (deviceEl == null) return false;

                var btn = SearchForButton(win, deviceEl, action);
                if (btn == null)
                {
                    TryInvokeElement(deviceEl);
                    await Task.Delay(700);
                    btn = SearchForButton(win, deviceEl, action);
                }
                if (btn == null)
                {
                    return false;
                }

                TryInvokeElement(btn);

                if (openedByApp)
                {
                    try
                    {
                        await Task.Delay(500);
                        win.SetFocus();
                        if (win.GetCurrentPattern(WindowPattern.Pattern) is WindowPattern cp) cp.Close();
                    }
                       catch { }
                }
                return true;
            }
               catch { return false; }
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
            var cond = new PropertyCondition(AutomationElement.ClassNameProperty, "ApplicationFrameWindow");
            foreach (AutomationElement w in AutomationElement.RootElement.FindAll(TreeScope.Children, cond))
                if ((w.Current.Name ?? "").Contains("Settings", StringComparison.OrdinalIgnoreCase)) return w;
            return null;
        }

        private async Task<AutomationElement?> FindElementByNameAsync(AutomationElement root, string name, int timeoutMs)
        {
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeoutMs)
            {
                var el = FindElementByName(root, name);
                if (el != null) return el;
                await Task.Delay(400);
            }
            return null;
        }

        private AutomationElement? FindElementByName(AutomationElement root, string name)
        {
            try
            {
                return root.FindFirst(TreeScope.Descendants,
                    new PropertyCondition(AutomationElement.NameProperty, name, PropertyConditionFlags.IgnoreCase));
            }
            catch (Exception ex) { return null; }
        }

        private AutomationElement? SearchForButton(AutomationElement win, AutomationElement deviceEl, string btnName)
        {
            var current = deviceEl;
            for (int i = 0; i < 6; i++)
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
            return FindButtonNamed(win, btnName);
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
