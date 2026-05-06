using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace GreenroomConnector.Services
{
    // Resolves the OIDC end_session_endpoint from a previously captured
    // authorize URL by hitting the provider's discovery document
    // ({issuer}/.well-known/openid-configuration). Authorize URL has shape
    // {issuer}/<provider-specific>?client_id=...&... — we strip a few known
    // suffixes to recover the issuer, then ask discovery for the truth.
    public static class OidcDiscovery
    {
        private static readonly string[] AuthorizePathSuffixes =
        {
            "/protocol/openid-connect/auth",        // Keycloak
            "/oauth2/authorize",                    // Auth0, Okta, Cognito
            "/oauth2/v1/authorize",                 // Okta (some tenants)
            "/oauth2/v2.0/authorize",               // Azure AD v2
            "/connect/authorize",                   // IdentityServer
            "/authorize"                            // bare fallback
        };

        public class LogoutEndpoint
        {
            public string EndSessionEndpoint { get; set; }
            public string ClientId { get; set; }
        }

        // Returns null if the authorize URL can't be parsed or no discovery
        // document responds. Caller is expected to fall back to a local-only
        // sign-out and surface a degraded-state message.
        public static async Task<LogoutEndpoint> ResolveAsync(string authorizeUrl)
        {
            if (string.IsNullOrEmpty(authorizeUrl)) return null;
            if (!Uri.TryCreate(authorizeUrl, UriKind.Absolute, out var uri)) return null;

            var clientId = ExtractQueryParameter(uri.Query, "client_id");
            if (string.IsNullOrEmpty(clientId)) return null;

            foreach (var issuer in EnumerateIssuerCandidates(uri))
            {
                var endSession = await TryDiscoverEndSessionAsync(issuer).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(endSession))
                {
                    return new LogoutEndpoint
                    {
                        EndSessionEndpoint = endSession,
                        ClientId = clientId
                    };
                }
            }
            return null;
        }

        private static IEnumerable<string> EnumerateIssuerCandidates(Uri authorizeUri)
        {
            // Reconstruct without query / fragment.
            var pathBase = authorizeUri.GetLeftPart(UriPartial.Path);

            foreach (var suffix in AuthorizePathSuffixes)
            {
                if (pathBase.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    yield return pathBase.Substring(0, pathBase.Length - suffix.Length);
            }

            // Last resort: the origin. Works for IdPs whose discovery lives
            // at the root (rare but spec-permitted).
            yield return authorizeUri.GetLeftPart(UriPartial.Authority);
        }

        private static async Task<string> TryDiscoverEndSessionAsync(string issuer)
        {
            if (string.IsNullOrEmpty(issuer)) return null;

            var trimmed = issuer.TrimEnd('/');
            var discoveryUrl = trimmed + "/.well-known/openid-configuration";

            try
            {
                using (var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                using (var response = await client.GetAsync(discoveryUrl).ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode) return null;
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var doc = JObject.Parse(body);
                    return (string)doc["end_session_endpoint"];
                }
            }
            catch (System.Exception ex)
            {
                DebugLog.Write("OIDC discovery against " + discoveryUrl + " failed: " + ex.Message);
                return null;
            }
        }

        private static string ExtractQueryParameter(string query, string name)
        {
            if (string.IsNullOrEmpty(query)) return null;
            // Query starts with '?'.
            var pairs = query.TrimStart('?').Split('&');
            foreach (var pair in pairs)
            {
                var eq = pair.IndexOf('=');
                if (eq <= 0) continue;
                var key = Uri.UnescapeDataString(pair.Substring(0, eq));
                if (!string.Equals(key, name, StringComparison.Ordinal)) continue;
                return Uri.UnescapeDataString(pair.Substring(eq + 1));
            }
            return null;
        }
    }
}
