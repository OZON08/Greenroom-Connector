using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using GreenroomConnector.Resources;
using GreenroomConnector.Services;

namespace GreenroomConnector.UI
{
    // Drives RP-initiated logout against the OIDC provider so the IdP-side
    // SSO session is killed, not just the local Greenlight cookie.
    //
    // Flow:
    //   1. Navigate to <endSession>?client_id=<cid>&post_logout_redirect_uri=<greenlight>/.
    //      The IdP clears its session cookie and 302s back to the post-logout URL.
    //   2. We close as soon as a navigation completes on the Greenlight host —
    //      that's the IdP's redirect arriving home.
    //   3. Hard timeout after 10 s closes the window even if the redirect
    //      never lands (e.g. IdP forces a confirmation page the user ignores
    //      or a network issue). Caller still proceeds with local cleanup.
    //
    // Shares the WebView2 user-data folder with LoginWindow so we operate on
    // the same browser-storage profile that holds the IdP SSO cookie.
    public partial class LogoutWindow : Form
    {
        private readonly Uri _greenlightUrl;
        private readonly string _endSessionEndpoint;
        private readonly string _clientId;
        private System.Windows.Forms.Timer _hardTimeout;

        public bool ServerLogoutCompleted { get; private set; }

        public LogoutWindow(Uri greenlightUrl, string endSessionEndpoint, string clientId)
        {
            _greenlightUrl = greenlightUrl ?? throw new ArgumentNullException(nameof(greenlightUrl));
            _endSessionEndpoint = endSessionEndpoint ?? throw new ArgumentNullException(nameof(endSessionEndpoint));
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            InitializeComponent();
            Icon = AppIcon.Load();
            ShowIcon = true;
            Text = Strings.Logout_Title;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GreenroomConnector", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
                await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            }
            catch (System.Exception ex)
            {
                DebugLog.Write("LogoutWindow: WebView2 init failed: " + ex.Message);
                Close();
                return;
            }

            webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            var postLogoutRedirect = _greenlightUrl.GetLeftPart(UriPartial.Authority) + "/";
            var url = AppendQuery(_endSessionEndpoint,
                "client_id=" + Uri.EscapeDataString(_clientId)
                + "&post_logout_redirect_uri=" + Uri.EscapeDataString(postLogoutRedirect));

            DebugLog.Write("LogoutWindow: navigating to end_session: " + url);
            webView.CoreWebView2.Navigate(url);

            _hardTimeout = new System.Windows.Forms.Timer { Interval = 10_000 };
            _hardTimeout.Tick += (s, args) =>
            {
                _hardTimeout.Stop();
                DebugLog.Write("LogoutWindow: hard timeout reached, closing without confirmed redirect.");
                Close();
            };
            _hardTimeout.Start();
        }

        private void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!Uri.TryCreate(webView.Source?.ToString(), UriKind.Absolute, out var current)) return;
            if (!string.Equals(current.Host, _greenlightUrl.Host, StringComparison.OrdinalIgnoreCase)) return;

            ServerLogoutCompleted = true;
            DebugLog.Write("LogoutWindow: post-logout redirect landed at " + current);
            Close();
        }

        private static string AppendQuery(string url, string query)
        {
            if (string.IsNullOrEmpty(query)) return url;
            return url + (url.IndexOf('?') >= 0 ? "&" : "?") + query;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _hardTimeout?.Stop();
            _hardTimeout?.Dispose();
            _hardTimeout = null;
            base.OnFormClosed(e);
        }
    }
}
