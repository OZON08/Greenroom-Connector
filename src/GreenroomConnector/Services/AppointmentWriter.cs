using System;
using System.Reflection;
using GreenroomConnector.Models;
using GreenroomConnector.Resources;
using Outlook = Microsoft.Office.Interop.Outlook;

namespace GreenroomConnector.Services
{
    public class AppointmentWriter
    {
        private readonly SettingsProvider _settings;

        public AppointmentWriter(SettingsProvider settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        // Writes the join link as plain text into the appointment body and a
        // configurable label into Location (when empty). Outlook auto-detects
        // URLs in plain Body and renders them as clickable links — works for
        // the organizer and for every recipient regardless of mail client.
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
                WriteCore(appointment, room,
                    _settings.LocationText,
                    _settings.ShowDialIn,
                    _settings.DialInNumber);
                DebugLog.Write("InsertMeetingLink OK for room " + (room.Name ?? room.FriendlyId));
            }
            catch (Exception ex)
            {
                DebugLog.Write("InsertMeetingLink FAILED: " + ex.GetType().Name + ": " + ex.Message
                    + Environment.NewLine + ex.StackTrace);
                throw;
            }
        }

        private static void WriteCore(object apt, Room room,
            string locationTemplate, bool showDialIn, string dialInNumber)
        {
            string existing = SafeGetString(apt, "Body");
            SetProperty(apt, "Body",
                BuildPlainBlock(room, showDialIn, dialInNumber) + Environment.NewLine + existing);

            // Only fill Location if (a) admin configured a template AND
            // (b) the user hasn't already typed something there.
            if (string.IsNullOrWhiteSpace(locationTemplate)) return;

            string currentLocation = SafeGetString(apt, "Location");
            if (!string.IsNullOrWhiteSpace(currentLocation)) return;

            string locationText = ApplyTemplate(locationTemplate, room, dialInNumber);
            TrySetProperty(apt, "Location", locationText);
        }

        private static string BuildPlainBlock(Room room, bool showDialIn, string dialInNumber)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("----------------------------------------");
            sb.AppendLine(Strings.Meeting_Header);
            sb.AppendLine(Strings.Meeting_Room + ": " + (room.Name ?? room.FriendlyId));
            sb.AppendLine(Strings.Meeting_JoinLinkText + ": " + room.JoinUrl);

            // Dial-in section only when admin enabled it AND a number is configured.
            // Avoids printing a "Rufnummer:" line with no number behind it.
            bool dialInPossible = showDialIn
                && !string.IsNullOrWhiteSpace(dialInNumber)
                && !string.IsNullOrWhiteSpace(Strings.Meeting_DialIn);
            if (dialInPossible)
            {
                sb.AppendLine();
                sb.AppendLine(ApplyTemplate(Strings.Meeting_DialIn, room, dialInNumber));
            }

            sb.AppendLine("----------------------------------------");
            return sb.ToString();
        }

        private static string ApplyTemplate(string template, Room room, string dialInNumber)
        {
            string roomName = room.Name ?? room.FriendlyId ?? string.Empty;
            return template
                .Replace("{room}", roomName)
                .Replace("{number}", dialInNumber ?? string.Empty);
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
            catch (Exception ex) { DebugLog.Write("SetProperty(" + name + ") failed: " + ex.Message); }
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
    }
}
