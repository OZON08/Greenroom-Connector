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
                return Uri.TryCreate(raw, UriKind.Absolute, out var uri) ? uri : null;
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
