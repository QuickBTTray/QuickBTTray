using System.Runtime.InteropServices;
using Windows.Media.Control;

namespace QuickBTTrayApp.Services
{
    internal sealed record MediaControlDispatchResult(bool Success, string DiagnosticMessage);

    /// <summary>
    /// Sends media play/pause using a hybrid strategy:
    /// - SMTC is queried for playback state only (guards against toggling a stopped app).
    /// - SendInput with VK_MEDIA_PLAY_PAUSE (0xB3) is used for delivery. This simulates
    ///   the hardware media key, which Windows routes to the current media session (last
    ///   active app). Works for Chrome, Spotify, and all other media apps. WM_APPCOMMAND
    ///   broadcast is NOT used because it targets all windows and Chrome ignores it.
    /// </summary>
    internal static class GlobalMediaControlService
    {
        private const uint   InputKeyboard        = 1;
        private const uint   KeyeventfExtendedkey = 0x0001;
        private const uint   KeyeventfKeyup       = 0x0002;
        private const ushort VkMediaPlayPause     = 0xB3;  // hardware Play/Pause toggle

        // INPUT union must be at least as large as the biggest member (MOUSEINPUT = 28 bytes on x64)
        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint Type; public INPUTUNION Union; }

        [StructLayout(LayoutKind.Explicit, Size = 28)]
        private struct INPUTUNION { [FieldOffset(0)] public KEYBDINPUT Keyboard; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT
        {
            public ushort VirtualKey;
            public ushort ScanCode;
            public uint   Flags;
            public uint   Time;
            public nint   ExtraInfo;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

        /// <summary>
        /// On disconnect: pause if (and only if) something is currently playing.
        /// Skips silently when nothing is playing — avoids toggling a stopped app into playing.
        /// </summary>
        public static async Task<MediaControlDispatchResult> TrySendPauseAsync()
        {
            try
            {
                var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (mgr is null)
                    return SendMediaToggle("Pause", "SMTC unavailable — sent unconditionally");

                var playingCount = mgr.GetSessions()
                    .Count(s => s.GetPlaybackInfo()?.PlaybackStatus ==
                        GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);

                if (playingCount == 0)
                    return new(false, "Pause: nothing playing — skipped");

                return SendMediaToggle("Pause", $"SMTC: {playingCount} playing → pausing");
            }
            catch (Exception ex)
            {
                return SendMediaToggle("Pause", $"SMTC error: {ex.Message}");
            }
        }

        /// <summary>
        /// On connect: resume only if nothing is currently playing and at least one
        /// session is paused. If something is already playing (e.g. through speakers)
        /// we leave it alone.
        /// </summary>
        public static async Task<MediaControlDispatchResult> TrySendPlayAsync()
        {
            try
            {
                var mgr = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
                if (mgr is null)
                    return SendMediaToggle("Play", "SMTC unavailable — sent unconditionally");

                var sessions     = mgr.GetSessions();
                var playingCount = sessions.Count(s => s.GetPlaybackInfo()?.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing);
                var pausedCount  = sessions.Count(s => s.GetPlaybackInfo()?.PlaybackStatus ==
                    GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused);

                if (playingCount > 0)
                    return new(false, $"Play: {playingCount} session(s) already playing — skipped");

                if (pausedCount == 0)
                    return new(false, "Play: nothing paused — skipped");

                return SendMediaToggle("Play", $"SMTC: {pausedCount} paused, 0 playing → resuming");
            }
            catch (Exception ex)
            {
                return SendMediaToggle("Play", $"SMTC error: {ex.Message}");
            }
        }

        // ── SendInput delivery ─────────────────────────────────────────────────────

        private static MediaControlDispatchResult SendMediaToggle(string direction, string context)
        {
            var keyDown = MakeKeyInput(KeyeventfExtendedkey);
            var keyUp   = MakeKeyInput(KeyeventfExtendedkey | KeyeventfKeyup);
            var sent    = SendInput(2, [keyDown, keyUp], Marshal.SizeOf<INPUT>());
            return sent == 2
                ? new(true,  $"{direction}: media key sent ({context})")
                : new(false, $"{direction}: SendInput failed sent={sent} err={Marshal.GetLastWin32Error()} ({context})");
        }

        private static INPUT MakeKeyInput(uint flags) => new()
        {
            Type  = InputKeyboard,
            Union = new INPUTUNION
            {
                Keyboard = new KEYBDINPUT { VirtualKey = VkMediaPlayPause, Flags = flags }
            }
        };
    }
}
