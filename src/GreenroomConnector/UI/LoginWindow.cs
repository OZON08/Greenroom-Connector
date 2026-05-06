using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using GreenroomConnector.Resources;
using GreenroomConnector.Services;

namespace GreenroomConnector.UI
{
    // Hosts Keycloak-driven Greenlight login in a WebView2.
    //
    // Flow:
    //   1. Navigate to <greenlightUrl>?sso=true. The Greenlight SPA auto-submits
    //      an OmniAuth form which redirects to Keycloak's authorize endpoint.
    //   2. User authenticates in Keycloak (any method: password, MFA, etc.).
    //   3. Keycloak redirects back to /auth/:provider/callback where Greenlight
    //      sets the _greenlight-3_0_session cookie and redirects to the SPA.
    //   4. A timer polls /api/v1/rooms.json from inside the WebView every 1.5s.
    //      200 means we are authenticated; 401 means still logged out. Polling
    //      is far more robust than reacting to NavigationCompleted events in a SPA.
    //   5. On success, extract the _greenlight-3_0_session cookie and close.
    public partial class LoginWindow : Form
    {
        private readonly Uri _greenlightUrl;
        private System.Windows.Forms.Timer _authPollTimer;
        private bool _capturing;

        public string SessionCookie { get; private set; }

        // Captured on the first navigation that leaves the Greenlight host and
        // carries a client_id query parameter — i.e. the OIDC authorize request.
        // Persisted by the caller after a successful login so a later sign-out
        // can reach the same IdP's end_session_endpoint via discovery.
        public string AuthorizeUrl { get; private set; }

        public LoginWindow(Uri greenlightUrl)
        {
            _greenlightUrl = greenlightUrl ?? throw new ArgumentNullException(nameof(greenlightUrl));
            InitializeComponent();
            Icon = AppIcon.Load();
            ShowIcon = true;
            Text = Strings.Login_Title;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "GreenroomConnector", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
            await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);

            webView.CoreWebView2.NavigationStarting += OnNavigationStarting;

            var startUrl = new Uri(_greenlightUrl, "/?sso=true").ToString();
            webView.CoreWebView2.Navigate(startUrl);

            _authPollTimer = new System.Windows.Forms.Timer { Interval = 1500 };
            _authPollTimer.Tick += AuthPollTimer_Tick;
            _authPollTimer.Start();
        }

        private void OnNavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (AuthorizeUrl != null) return;
            if (!Uri.TryCreate(e.Uri, UriKind.Absolute, out var uri)) return;
            if (string.Equals(uri.Host, _greenlightUrl.Host, StringComparison.OrdinalIgnoreCase)) return;
            // Authorize requests always carry a client_id query parameter
            // (RFC 6749 §4.1.1). Without it we may be looking at a
            // pre-auth landing page or a static asset on the IdP host.
            if (uri.Query?.IndexOf("client_id=", StringComparison.Ordinal) < 0) return;

            AuthorizeUrl = uri.ToString();
        }

        private async void AuthPollTimer_Tick(object sender, EventArgs e)
        {
            if (_capturing || webView?.CoreWebView2 == null) return;

            // Don't probe while we're on the Keycloak (or any other) host —
            // /api/v1/rooms.json doesn't exist there.
            if (!Uri.TryCreate(webView.Source?.ToString(), UriKind.Absolute, out var current)) return;
            if (!string.Equals(current.Host, _greenlightUrl.Host, StringComparison.OrdinalIgnoreCase)) return;

            _capturing = true;
            try
            {
                if (await IsAuthenticatedAsync().ConfigureAwait(true))
                {
                    _authPollTimer.Stop();
                    await CaptureAndCloseAsync().ConfigureAwait(true);
                }
            }
            catch
            {
                // Swallow per-tick exceptions; next tick will retry.
            }
            finally
            {
                _capturing = false;
            }
        }

        private async Task<bool> IsAuthenticatedAsync()
        {
            // Synchronous XMLHttpRequest inside the WebView returns the HTTP
            // status directly to ExecuteScriptAsync, sidestepping the Promise
            // serialisation pitfalls of fetch().
            const string script = @"
                (function () {
                  try {
                    var xhr = new XMLHttpRequest();
                    xhr.open('GET', '/api/v1/rooms.json', false);
                    xhr.setRequestHeader('Accept', 'application/json');
                    xhr.send(null);
                    return String(xhr.status);
                  } catch (e) { return 'error'; }
                })()";

            var raw = await webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
            var trimmed = (raw ?? string.Empty).Trim('"');
            return trimmed == "200";
        }

        private async Task CaptureAndCloseAsync()
        {
            var origin = _greenlightUrl.GetLeftPart(UriPartial.Authority);
            var cookies = await webView.CoreWebView2.CookieManager
                .GetCookiesAsync(origin).ConfigureAwait(true);

            var relevant = cookies
                .Where(c => string.Equals(c.Name, GreenlightClient.SessionCookieName, StringComparison.Ordinal)
                            && !string.IsNullOrEmpty(c.Value))
                .Select(c => $"{c.Name}={c.Value}")
                .ToList();

            if (relevant.Count == 0) return;

            SessionCookie = string.Join("; ", relevant);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _authPollTimer?.Stop();
            _authPollTimer?.Dispose();
            _authPollTimer = null;
            base.OnFormClosed(e);
        }
    }
}
