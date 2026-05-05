using System;
using Microsoft.Win32;

namespace GreenroomConnector.Services
{
    public class SettingsProvider
    {
        private const string HklmKey = @"SOFTWARE\GreenroomConnector";

        public Uri GreenlightUrl
        {
            get
            {
                var raw = ReadHklm("GreenlightUrl");
                if (string.IsNullOrWhiteSpace(raw)) return null;
                if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)) return null;

                // Disallow plaintext HTTP for non-loopback hosts: the session
                // cookie is sent on every API call and the WebView2 login flow
                // would also travel in the clear. Loopback (localhost,
                // 127.0.0.0/8, ::1) stays allowed so the local dev stack works.
                if (uri.Scheme != Uri.UriSchemeHttps && !uri.IsLoopback) return null;
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) return null;

                return uri;
            }
        }

        // Master switch for the verbose file logger under
        // %LocalAppData%\GreenroomConnector\debug.log. Off by default — when
        // enabled, full HTTP response bodies and exception stacks are written.
        // REG_SZ "true"/"1" enables; anything else disables.
        public bool DebugLogging
        {
            get
            {
                var raw = ReadHklm("DebugLogging");
                return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                    || raw == "1";
            }
        }

        public string Language => ReadHklm("Language") ?? "auto";

        public string InstallDir => ReadHklm("InstallDir") ?? string.Empty;

        // Optional admin-configured text written into the appointment Location
        // field. Empty/missing means: do not touch Location. Supports the
        // placeholder {room} which is substituted with the selected room name.
        public string LocationText => ReadHklm("LocationText") ?? string.Empty;

        // Switch (REG_SZ "true"/"false") that controls whether the localized
        // phone dial-in text from the resx (Strings.Meeting_DialIn) is
        // appended to the appointment body. Default: off.
        public bool ShowDialIn
        {
            get
            {
                var raw = ReadHklm("ShowDialIn");
                return string.Equals(raw, "true", StringComparison.OrdinalIgnoreCase)
                    || raw == "1";
            }
        }

        // Deployment-specific dial-in phone number. Substituted into
        // Strings.Meeting_DialIn via the placeholder {number}.
        public string DialInNumber => ReadHklm("DialInNumber") ?? string.Empty;

        private static string ReadHklm(string name)
        {
            using (var key = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64)
                       .OpenSubKey(HklmKey))
            {
                return key?.GetValue(name) as string;
            }
        }
    }
}
