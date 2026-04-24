using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;
using OutlookGreenlight.AddIn.Resources;
using OutlookGreenlight.AddIn.Services;

namespace OutlookGreenlight.AddIn.UI
{
    // Hosts Keycloak-driven Greenlight login in a WebView2.
    //
    // Flow:
    //   1. Navigate to <greenlightUrl>?sso=true. The Greenlight SPA auto-submits
    //      an OmniAuth form which redirects to Keycloak's authorize endpoint.
    //   2. User authenticates in Keycloak (any method: password, MFA, etc.).
    //   3. Keycloak redirects back to /auth/:provider/callback where Greenlight
    //      sets the _greenlight-3_0_session cookie and redirects to the SPA.
    //   4. After each navigation we ping /api/v1/rooms.json from inside the
    //      WebView. 200 means we are authenticated; 401 means still logged out.
    //      This is far more robust than matching URLs in a SPA.
    //   5. On success, extract the _greenlight-3_0_session cookie and close.
    public partial class LoginWindow : Form
    {
        private readonly Uri _greenlightUrl;
        private bool _capturing;

        public string SessionCookie { get; private set; }

        public LoginWindow(Uri greenlightUrl)
        {
            _greenlightUrl = greenlightUrl ?? throw new ArgumentNullException(nameof(greenlightUrl));
            InitializeComponent();
            Text = Strings.Login_Title;
        }

        protected override async void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            var userDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Greenlight", "OutlookIntegration", "WebView2");
            Directory.CreateDirectory(userDataFolder);

            var env = await CoreWebView2Environment.CreateAsync(null, userDataFolder).ConfigureAwait(true);
            await webView.EnsureCoreWebView2Async(env).ConfigureAwait(true);

            webView.CoreWebView2.NavigationCompleted += OnNavigationCompleted;

            var startUrl = new Uri(_greenlightUrl, "/?sso=true").ToString();
            webView.CoreWebView2.Navigate(startUrl);
        }

        private async void OnNavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!e.IsSuccess || _capturing) return;

            // Only probe after we've returned to a Greenlight-origin page (skip Keycloak pages).
            if (!Uri.TryCreate(webView.Source?.ToString(), UriKind.Absolute, out var current))
                return;
            if (!string.Equals(current.Host, _greenlightUrl.Host, StringComparison.OrdinalIgnoreCase))
                return;

            _capturing = true;
            try
            {
                if (await IsAuthenticatedAsync().ConfigureAwait(true))
                    await CaptureAndCloseAsync().ConfigureAwait(true);
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format(Strings.Error_Unexpected, ex.Message),
                    Strings.App_Name, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _capturing = false;
            }
        }

        private async Task<bool> IsAuthenticatedAsync()
        {
            // Evaluates a fetch inside the WebView so the browser's own cookies
            // are used (origin-scoped, HttpOnly-aware). Returns the HTTP status
            // as a string, "error", or similar.
            const string script = @"
                (async () => {
                  try {
                    const r = await fetch('/api/v1/rooms.json', {
                      credentials: 'same-origin',
                      headers: { 'Accept': 'application/json' }
                    });
                    return String(r.status);
                  } catch (e) { return 'error'; }
                })()";

            var raw = await webView.CoreWebView2.ExecuteScriptAsync(script).ConfigureAwait(true);
            // ExecuteScriptAsync returns JSON-encoded result, e.g. "\"200\"".
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
    }
}
