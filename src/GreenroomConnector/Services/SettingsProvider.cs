using System;
using Microsoft.Win32;

namespace GreenroomConnector.Services
{
    public class SettingsProvider
    {
        private const string HklmKey = @"SOFTWARE\Greenlight\OutlookIntegration";

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
