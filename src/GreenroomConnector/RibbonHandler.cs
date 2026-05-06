using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Office.Core;
using GreenroomConnector.Resources;
using GreenroomConnector.Services;
using GreenroomConnector.UI;
using Outlook = Microsoft.Office.Interop.Outlook;
using stdole;

namespace GreenroomConnector
{
    [ComVisible(true)]
    public class RibbonHandler : IRibbonExtensibility
    {
        private IRibbonUI _ribbon;

        public string GetCustomUI(string ribbonID)
        {
            const string resource = "GreenroomConnector.Ribbon.xml";
            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource))
            {
                if (stream == null)
                    throw new InvalidOperationException(
                        "Embedded ribbon resource '" + resource + "' not found.");
                using (var reader = new StreamReader(stream))
                    return reader.ReadToEnd();
            }
        }

        public void OnRibbonLoad(IRibbonUI ribbon) => _ribbon = ribbon;

        public string OnGetGroupLabel(IRibbonControl control) => Strings.Ribbon_GroupLabel;
        public string OnGetButtonLabel(IRibbonControl control) => Strings.Ribbon_ButtonLabel;
        public string OnGetButtonSupertip(IRibbonControl control) => Strings.Ribbon_ButtonSupertip;
        public string OnGetSignOutLabel(IRibbonControl control) => Strings.Ribbon_SignOutLabel;
        public string OnGetSignOutSupertip(IRibbonControl control) => Strings.Ribbon_SignOutSupertip;

        // Office Ribbon getImage callback. Returns the embedded PNG as an
        // IPictureDisp — the COM type the Office customUI expects. Bitmap
        // alone works on some Office builds but IPictureDisp is the safe path.
        public IPictureDisp OnGetButtonImage(IRibbonControl control) => RibbonImageProvider.Get();

        public void OnInsertGreenlightLink(IRibbonControl control)
        {
            try
            {
                var appointment = ResolveAppointment(control);
                if (appointment == null)
                {
                    MessageBox.Show(Strings.Error_NoAppointmentContext,
                        Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                using (var picker = new RoomPickerForm())
                {
                    if (picker.ShowDialog() != DialogResult.OK || picker.SelectedRoom == null)
                        return;

                    ThisAddIn.Instance.Writer.InsertMeetingLink(appointment, picker.SelectedRoom);
                }
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Error_Unexpected, ex.Message),
                    Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static Outlook.AppointmentItem ResolveAppointment(IRibbonControl control)
        {
            if (control?.Context is Outlook.Inspector inspector
                && inspector.CurrentItem is Outlook.AppointmentItem fromInspector)
            {
                return fromInspector;
            }

            var app = ThisAddIn.Instance?.Application;
            return app?.ActiveInspector()?.CurrentItem as Outlook.AppointmentItem;
        }

        // Sign-out:
        //   1. RP-initiated OIDC logout via a transient WebView2 (LogoutWindow).
        //      Reads the authorize URL captured at login, resolves the IdP's
        //      end_session_endpoint via discovery, navigates there with
        //      client_id + post_logout_redirect_uri so the IdP can validate
        //      the call without an id_token. Best-effort: any failure (no
        //      authorize URL stored, discovery 404, navigation timeout) only
        //      degrades to local-only sign-out.
        //   2. Local cleanup runs unconditionally after step 1: clears the
        //      DPAPI cookie blob in HKCU and wipes the WebView2 user-data folder.
        public void OnSignOut(IRibbonControl control)
        {
            var serverLogoutOk = false;
            try
            {
                serverLogoutOk = TryServerLogout();
            }
            catch (System.Exception ex)
            {
                DebugLog.Write("Server-side sign-out attempt threw: " + ex.Message);
            }

            try
            {
                ThisAddIn.Instance?.Session?.Clear();
                TryDeleteWebView2UserData();

                var message = serverLogoutOk
                    ? Strings.SignOut_DoneMessage
                    : Strings.SignOut_LocalOnlyMessage;
                MessageBox.Show(message, Strings.App_Name,
                    MessageBoxButtons.OK,
                    serverLogoutOk ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(
                    string.Format(Strings.Error_Unexpected, ex.Message),
                    Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static bool TryServerLogout()
        {
            var addin = ThisAddIn.Instance;
            if (addin == null) return false;

            var greenlightUrl = addin.Settings?.GreenlightUrl;
            var authorizeUrl = addin.Session?.ReadAuthorizeUrl();
            if (greenlightUrl == null || string.IsNullOrEmpty(authorizeUrl))
            {
                DebugLog.Write("Server-side sign-out skipped: no authorize URL stored or no GreenlightUrl.");
                return false;
            }

            // Discovery is async; .Result is acceptable here because we're on
            // the UI thread, the call has a 5 s timeout per candidate, and
            // there's no SynchronizationContext deadlock risk in OidcDiscovery
            // (it uses ConfigureAwait(false) throughout).
            var endpoint = OidcDiscovery.ResolveAsync(authorizeUrl).GetAwaiter().GetResult();
            if (endpoint == null || string.IsNullOrEmpty(endpoint.EndSessionEndpoint))
            {
                DebugLog.Write("Server-side sign-out skipped: end_session_endpoint not discoverable.");
                return false;
            }

            using (var window = new LogoutWindow(greenlightUrl, endpoint.EndSessionEndpoint, endpoint.ClientId))
            {
                window.ShowDialog();
                return window.ServerLogoutCompleted;
            }
        }

        private static void TryDeleteWebView2UserData()
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GreenroomConnector", "WebView2");

            try
            {
                if (Directory.Exists(folder))
                    Directory.Delete(folder, recursive: true);
            }
            catch (System.Exception ex)
            {
                // The msedgewebview2.exe host can briefly hold a lock on the
                // SQLite cookie store after LoginWindow closes. Deletion is
                // best-effort; in the worst case the folder is recreated /
                // overwritten on the next sign-in.
                DebugLog.Write("WebView2 user-data delete failed: " + ex.Message);
            }
        }
    }
}
