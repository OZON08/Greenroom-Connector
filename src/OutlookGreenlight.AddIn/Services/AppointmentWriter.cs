using System;
using System.Net;
using Microsoft.Office.Interop.Outlook;
using OutlookGreenlight.AddIn.Models;
using OutlookGreenlight.AddIn.Resources;

namespace OutlookGreenlight.AddIn.Services
{
    public class AppointmentWriter
    {
        public void InsertMeetingLink(AppointmentItem appointment, Room room)
        {
            if (appointment == null) throw new ArgumentNullException(nameof(appointment));
            if (room == null) throw new ArgumentNullException(nameof(room));
            if (string.IsNullOrWhiteSpace(room.JoinUrl))
                throw new InvalidOperationException("Room has no join URL.");

            var marker = "<!-- greenlight-meeting -->";
            var htmlBlock = BuildHtmlBlock(room, marker);

            if (appointment.BodyFormat == OlBodyFormat.olFormatHTML)
            {
                var existing = appointment.HTMLBody ?? string.Empty;
                existing = StripExistingBlock(existing, marker);
                appointment.HTMLBody = htmlBlock + existing;
            }
            else
            {
                var existing = appointment.Body ?? string.Empty;
                var plain = BuildPlainBlock(room);
                appointment.Body = plain + Environment.NewLine + existing;
            }

            if (string.IsNullOrWhiteSpace(appointment.Location))
                appointment.Location = room.JoinUrl;
        }

        private static string BuildHtmlBlock(Room room, string marker)
        {
            var encodedUrl = WebUtility.HtmlEncode(room.JoinUrl);
            var encodedName = WebUtility.HtmlEncode(room.Name ?? room.FriendlyId ?? string.Empty);
            return
                marker + "\r\n" +
                "<div style=\"font-family:Segoe UI,Arial,sans-serif;font-size:11pt;border-top:1px solid #ccc;border-bottom:1px solid #ccc;padding:8px 0;margin:8px 0;\">" +
                "<p style=\"margin:0 0 4px 0;\"><b>" + WebUtility.HtmlEncode(Strings.Meeting_Header) + "</b></p>" +
                "<p style=\"margin:0 0 4px 0;\">" + WebUtility.HtmlEncode(Strings.Meeting_Room) + ": " + encodedName + "</p>" +
                "<p style=\"margin:0;\"><a href=\"" + encodedUrl + "\">" + WebUtility.HtmlEncode(Strings.Meeting_JoinLinkText) + "</a></p>" +
                "</div>\r\n" +
                marker + "-end\r\n";
        }

        private static string BuildPlainBlock(Room room)
        {
            return
                "---" + Environment.NewLine +
                Strings.Meeting_Header + Environment.NewLine +
                Strings.Meeting_Room + ": " + (room.Name ?? room.FriendlyId) + Environment.NewLine +
                Strings.Meeting_JoinLinkText + ": " + room.JoinUrl + Environment.NewLine +
                "---" + Environment.NewLine;
        }

        private static string StripExistingBlock(string html, string marker)
        {
            var startIdx = html.IndexOf(marker, StringComparison.Ordinal);
            if (startIdx < 0) return html;
            var endMarker = marker + "-end";
            var endIdx = html.IndexOf(endMarker, startIdx, StringComparison.Ordinal);
            if (endIdx < 0) return html;
            endIdx += endMarker.Length;
            return html.Substring(0, startIdx) + html.Substring(endIdx);
        }
    }
}
