using System;
using System.IO;
using System.Reflection;
using GreenroomConnector.Models;
using GreenroomConnector.Resources;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace GreenroomConnector.Services
{
    public class AppointmentWriter
    {
        // Writes the join link as plain text into the appointment body and into
        // Location (when empty). Outlook auto-detects URLs in plain Body and
        // renders them as clickable links — works for the organizer and for
        // every recipient regardless of mail client.
        //
        // We intentionally don't write HTMLBody. In Office 2024 / 365 classic
        // the IDispatch surface of a live AppointmentItem rejects HTMLBody
        // assignments with DISP_E_UNKNOWNNAME, even though the property exists
        // on the type. Plain Body is the reliable path.
        public void InsertMeetingLink(Outlook.AppointmentItem appointment, Room room)
        {
            if (appointment == null) throw new ArgumentNullException(nameof(appointment));
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (string.IsNullOrWhiteSpace(room.JoinUrl))
                throw new InvalidOperationException("Room has no join URL.");

            try
            {
                WriteCore(appointment, room);
                Log("InsertMeetingLink OK for room " + (room.Name ?? room.FriendlyId));
            }
            catch (Exception ex)
            {
                Log("InsertMeetingLink FAILED: " + ex.GetType().Name + ": " + ex.Message
                    + Environment.NewLine + ex.StackTrace);
                throw;
            }
        }

        private static void WriteCore(object apt, Room room)
        {
            string existing = SafeGetString(apt, "Body");
            SetProperty(apt, "Body", BuildPlainBlock(room) + Environment.NewLine + existing);

            string currentLocation = SafeGetString(apt, "Location");
            if (string.IsNullOrWhiteSpace(currentLocation))
                TrySetProperty(apt, "Location", room.JoinUrl);
        }

        private static string BuildPlainBlock(Room room)
        {
            return
                "----------------------------------------" + Environment.NewLine +
                Strings.Meeting_Header + Environment.NewLine +
                Strings.Meeting_Room + ": " + (room.Name ?? room.FriendlyId) + Environment.NewLine +
                Strings.Meeting_JoinLinkText + ": " + room.JoinUrl + Environment.NewLine +
                "----------------------------------------" + Environment.NewLine;
        }

        private static object GetProperty(object com, string name)
        {
            return com.GetType().InvokeMember(name,
                BindingFlags.GetProperty,
                null, com, null);
        }

        private static void SetProperty(object com, string name, object value)
        {
            com.GetType().InvokeMember(name,
                BindingFlags.SetProperty,
                null, com, new[] { value });
        }

        private static void TrySetProperty(object com, string name, object value)
        {
            try { SetProperty(com, name, value); }
            catch (Exception ex) { Log("SetProperty(" + name + ") failed: " + ex.Message); }
        }

        private static string SafeGetString(object com, string name)
        {
            try
            {
                var v = GetProperty(com, name);
                return v == null ? string.Empty : Convert.ToString(v) ?? string.Empty;
            }
            catch { return string.Empty; }
        }

        private static void Log(string message)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GreenroomConnector");
                Directory.CreateDirectory(dir);
                File.AppendAllText(Path.Combine(dir, "debug.log"),
                    "[" + DateTime.UtcNow.ToString("O") + "] " + message + Environment.NewLine);
            }
            catch { /* never let logging break the call */ }
        }
    }
}
