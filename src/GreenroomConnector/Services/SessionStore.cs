using System;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;

namespace GreenroomConnector.Services
{
    public class SessionStore
    {
        private const string HkcuKey = @"Software\GreenroomConnector";
        private const string CookieValueName = "SessionCookie";
        private const string ExpiryValueName = "SessionExpiresAt";
        private const string AuthorizeUrlValueName = "OidcAuthorizeUrl";

        public string ReadCookie()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(HkcuKey))
            {
                if (key == null) return null;
                if (!(key.GetValue(CookieValueName) is byte[] encrypted)) return null;

                if (key.GetValue(ExpiryValueName) is long ticks)
                {
                    var expiry = new DateTime(ticks, DateTimeKind.Utc);
                    if (DateTime.UtcNow > expiry) return null;
                }

                try
                {
                    var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (CryptographicException)
                {
                    return null;
                }
            }
        }

        public void WriteCookie(string cookieHeader, TimeSpan? validFor = null)
        {
            if (string.IsNullOrEmpty(cookieHeader))
            {
                Clear();
                return;
            }

            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(cookieHeader), null, DataProtectionScope.CurrentUser);

            using (var key = Registry.CurrentUser.CreateSubKey(HkcuKey))
            {
                if (key == null) return;
                key.SetValue(CookieValueName, encrypted, RegistryValueKind.Binary);
                var expiry = DateTime.UtcNow + (validFor ?? TimeSpan.FromDays(14));
                key.SetValue(ExpiryValueName, expiry.Ticks, RegistryValueKind.QWord);
            }
        }

        // OIDC authorize URL captured during the login flow. Holds enough to
        // drive a single-logout against the same IdP later: client_id sits in
        // the query string, the issuer is derivable from the path. Stored
        // DPAPI-encrypted because the URL leaks the IdP host + client_id —
        // not as sensitive as the cookie, but no point exposing it in cleartext.
        public string ReadAuthorizeUrl()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(HkcuKey))
            {
                if (key == null) return null;
                if (!(key.GetValue(AuthorizeUrlValueName) is byte[] encrypted)) return null;

                try
                {
                    var plain = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
                    return Encoding.UTF8.GetString(plain);
                }
                catch (CryptographicException)
                {
                    return null;
                }
            }
        }

        public void WriteAuthorizeUrl(string authorizeUrl)
        {
            if (string.IsNullOrEmpty(authorizeUrl)) return;

            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(authorizeUrl), null, DataProtectionScope.CurrentUser);

            using (var key = Registry.CurrentUser.CreateSubKey(HkcuKey))
            {
                if (key == null) return;
                key.SetValue(AuthorizeUrlValueName, encrypted, RegistryValueKind.Binary);
            }
        }

        public void Clear()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(HkcuKey, writable: true))
            {
                if (key == null) return;
                key.DeleteValue(CookieValueName, throwOnMissingValue: false);
                key.DeleteValue(ExpiryValueName, throwOnMissingValue: false);
                key.DeleteValue(AuthorizeUrlValueName, throwOnMissingValue: false);
            }
        }
    }
}
