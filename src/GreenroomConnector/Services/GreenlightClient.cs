using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using GreenroomConnector.Models;

namespace GreenroomConnector.Services
{
    // Talks to Greenlight v3 API. Endpoints are under /api/v1 and require the
    // .json extension (see Greenlight's config/routes.rb). All calls are
    // authenticated by sending the _greenlight-3_0_session cookie captured via
    // the WebView2 login flow.
    public class GreenlightClient : IDisposable
    {
        public const string SessionCookieName = "_greenlight-3_0_session";

        private readonly SettingsProvider _settings;
        private readonly SessionStore _session;
        private HttpClient _http;
        private HttpClientHandler _handler;
        private string _currentUserId;

        public GreenlightClient(SettingsProvider settings, SessionStore session)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _session = session ?? throw new ArgumentNullException(nameof(session));
        }

        public bool HasSession => !string.IsNullOrEmpty(_session.ReadCookie());

        // Last raw response body from GetRoomsAsync — for diagnostics when the
        // parsed list is empty or unexpected.
        public string LastRoomsResponseBody { get; private set; }
        public int? LastRoomsStatusCode { get; private set; }
        public string LastRoomsRequestUri { get; private set; }
        public bool LastRoomsCookieSent { get; private set; }

        private HttpClient Http
        {
            get
            {
                if (_http != null) return _http;
                _handler = new HttpClientHandler
                {
                    UseCookies = false,
                    AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
                };
                _http = new HttpClient(_handler)
                {
                    BaseAddress = _settings.GreenlightUrl
                        ?? throw new InvalidOperationException("GreenlightUrl is not configured (HKLM)."),
                    Timeout = TimeSpan.FromSeconds(15)
                };
                _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
                return _http;
            }
        }

        public async Task<IReadOnlyList<Room>> GetRoomsAsync()
        {
            using (var request = BuildRequest(HttpMethod.Get, "api/v1/rooms.json"))
            {
                LastRoomsRequestUri = (Http.BaseAddress != null
                    ? new Uri(Http.BaseAddress, request.RequestUri).ToString()
                    : request.RequestUri?.ToString());
                LastRoomsCookieSent = request.Headers.Contains("Cookie");

                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    LastRoomsStatusCode = (int)response.StatusCode;
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    LastRoomsResponseBody = body;
                    DebugLog.Write("GET /api/v1/rooms.json -> HTTP " + (int)response.StatusCode
                        + Environment.NewLine + (body ?? "(empty)") + Environment.NewLine + "----");

                    if (response.StatusCode == HttpStatusCode.Unauthorized
                        || response.StatusCode == HttpStatusCode.Forbidden)
                    {
                        throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                    }

                    response.EnsureSuccessStatusCode();
                    return ParseRooms(body);
                }
            }
        }

        public async Task<Dictionary<string, string>> GetRoomsConfigurationsAsync()
        {
            using (var request = BuildRequest(HttpMethod.Get, "api/v1/rooms_configurations.json"))
            using (var response = await Http.SendAsync(request).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                DebugLog.Write("GET /api/v1/rooms_configurations.json -> HTTP " + (int)response.StatusCode);
                return ParseConfigurations(body);
            }
        }

        // GET /api/v1/sessions.json returns current_user including their integer id.
        // Cached for the lifetime of the client — cleared when Dispose is called.
        private async Task<string> EnsureCurrentUserIdAsync()
        {
            if (!string.IsNullOrEmpty(_currentUserId)) return _currentUserId;

            using (var request = BuildRequest(HttpMethod.Get, "api/v1/sessions.json"))
            using (var response = await Http.SendAsync(request).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var token = JToken.Parse(body);
                _currentUserId = (token as JObject)?["data"]?["id"]?.ToString();
                DebugLog.Write("GET /api/v1/sessions.json -> user_id=" + _currentUserId);
                return _currentUserId;
            }
        }

        public async Task<string> CreateRoomAsync(string name)
        {
            var userId = await EnsureCurrentUserIdAsync().ConfigureAwait(false);
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                new { room = new { name, user_id = userId } });
            using (var request = BuildRequest(HttpMethod.Post, "api/v1/rooms.json"))
            {
                request.Content = new System.Net.Http.StringContent(
                    json, System.Text.Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized
                        || response.StatusCode == HttpStatusCode.Forbidden)
                        throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    DebugLog.Write("POST /api/v1/rooms.json -> HTTP " + (int)response.StatusCode
                        + Environment.NewLine + body);
                    return ExtractFriendlyId(body);
                }
            }
        }

        public async Task UpdateRoomSettingAsync(string friendlyId, string settingName, string settingValue)
        {
            var json = Newtonsoft.Json.JsonConvert.SerializeObject(
                new { room_setting = new { settingName, settingValue } });
            using (var request = BuildRequest(new HttpMethod("PATCH"),
                $"api/v1/room_settings/{friendlyId}.json"))
            {
                request.Content = new System.Net.Http.StringContent(
                    json, System.Text.Encoding.UTF8, "application/json");
                using (var response = await Http.SendAsync(request).ConfigureAwait(false))
                {
                    DebugLog.Write($"PATCH /api/v1/room_settings/{friendlyId}.json"
                        + $" [{settingName}={settingValue}] -> HTTP " + (int)response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.Unauthorized
                        || response.StatusCode == HttpStatusCode.Forbidden)
                        throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                    response.EnsureSuccessStatusCode();
                }
            }
        }

        public async Task<Dictionary<string, string>> GetRoomSettingsAsync(string friendlyId)
        {
            using (var request = BuildRequest(HttpMethod.Get,
                $"api/v1/room_settings/{friendlyId}.json"))
            using (var response = await Http.SendAsync(request).ConfigureAwait(false))
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden)
                    throw new UnauthorizedAccessException("Greenlight session expired or missing.");
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                DebugLog.Write($"GET /api/v1/room_settings/{friendlyId}.json -> HTTP "
                    + (int)response.StatusCode);
                return ParseConfigurations(body);
            }
        }

        internal static Dictionary<string, string> ParseConfigurations(string json)
        {
            var token = JToken.Parse(json);
            var data = (token is JObject obj ? obj["data"] : token) as JObject ?? new JObject();
            return data.ToObject<Dictionary<string, string>>() ?? new Dictionary<string, string>();
        }

        internal static string ExtractFriendlyId(string json)
        {
            var token = JToken.Parse(json);
            var path = (token is JObject obj ? obj["data"]?.ToString() : null) ?? string.Empty;
            // path = "/rooms/abc-def-ghi"
            var segments = path.Trim('/').Split('/');
            if (segments.Length < 2 || string.IsNullOrEmpty(segments[1]))
                throw new InvalidOperationException("Unexpected room creation response: " + path);
            return segments[1];
        }

        private List<Room> ParseRooms(string json)
        {
            var token = JToken.Parse(json);
            // Greenlight wraps collections in {"data":[...], "meta":{}} via render_data helper.
            var array = (token is JObject obj ? obj["data"] : token) as JArray ?? new JArray();

            var baseUrl = _settings.GreenlightUrl;
            var result = new List<Room>();
            foreach (var item in array.OfType<JObject>())
            {
                var room = item.ToObject<Room>();
                if (room == null) continue;
                if (!string.IsNullOrEmpty(room.FriendlyId) && baseUrl != null)
                    room.JoinUrl = new Uri(baseUrl, $"/rooms/{room.FriendlyId}").ToString();
                result.Add(room);
            }
            return result;
        }

        private HttpRequestMessage BuildRequest(HttpMethod method, string relativeUri)
        {
            var request = new HttpRequestMessage(method, relativeUri);
            var cookie = _session.ReadCookie();
            if (!string.IsNullOrEmpty(cookie))
            {
                // TryAddWithoutValidation: standard Add() rejects values with chars
                // like '%' or '/' that are common in URL-encoded Rails session cookies.
                request.Headers.TryAddWithoutValidation("Cookie", cookie);
            }
            return request;
        }

        public void Dispose()
        {
            _http?.Dispose();
            _handler?.Dispose();
            _http = null;
            _handler = null;
        }
    }
}
