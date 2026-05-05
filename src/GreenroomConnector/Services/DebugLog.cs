using System;
using System.IO;

namespace GreenroomConnector.Services
{
    // Shared file logger for diagnostic output. Off by default.
    //
    // Gated by SettingsProvider.DebugLogging (HKLM:DebugLogging=true). When
    // disabled, Write() is a no-op. When enabled, lines are appended to
    // %LocalAppData%\GreenroomConnector\debug.log and the file is rotated
    // to debug.log.old once it crosses MaxFileSize, so the on-disk footprint
    // is bounded to roughly 2x that limit.
    //
    // This logger replaces the previous unconditional logging in
    // GreenlightClient.WriteDebugLog and AppointmentWriter.Log: that older
    // path persisted full /api/v1/rooms.json bodies (room IDs, owners,
    // friendly_ids — usable to deep-link into rooms) on every call, with no
    // size cap. Cf. security review M1.
    internal static class DebugLog
    {
        private const long MaxFileSize = 1L * 1024 * 1024; // 1 MB
        private static readonly object Sync = new object();

        private static SettingsProvider _settings;

        public static void Init(SettingsProvider settings)
        {
            _settings = settings;
        }

        public static bool IsEnabled => _settings?.DebugLogging == true;

        public static void Write(string message)
        {
            if (!IsEnabled) return;

            try
            {
                lock (Sync)
                {
                    var dir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "GreenroomConnector");
                    Directory.CreateDirectory(dir);

                    var path = Path.Combine(dir, "debug.log");
                    Rotate(path);

                    File.AppendAllText(path,
                        "[" + DateTime.UtcNow.ToString("O") + "] " + message + Environment.NewLine);
                }
            }
            catch
            {
                // Logging must never break the calling code path.
            }
        }

        private static void Rotate(string path)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length < MaxFileSize) return;

                var rotated = path + ".old";
                if (File.Exists(rotated)) File.Delete(rotated);
                File.Move(path, rotated);
            }
            catch
            {
                // If rotation fails (file in use, ACL, etc.) we keep appending
                // to the live file rather than dropping the message.
            }
        }
    }
}
