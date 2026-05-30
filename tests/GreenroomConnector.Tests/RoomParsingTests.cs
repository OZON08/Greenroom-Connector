using System.Linq;
using GreenroomConnector.Services;
using Newtonsoft.Json.Linq;
using Xunit;

namespace GreenroomConnector.Tests
{
    // Exercises the JSON shape contract derived from Greenlight v3's
    // RoomSerializer and render_data helper. Shape: { "data": [ ... ], "meta": {} }
    // with each room having id (long), name, friendly_id, online, participants,
    // last_session, and optional shared_owner.
    public class RoomParsingTests
    {
        private const string RealisticBody = @"{
            ""data"": [
                {
                    ""id"": 42,
                    ""name"": ""Team Standup"",
                    ""friendly_id"": ""abc-def-ghi-jkl"",
                    ""online"": false,
                    ""participants"": 0,
                    ""last_session"": ""2026-04-22T08:15:00.000Z""
                },
                {
                    ""id"": 57,
                    ""name"": ""Shared Room"",
                    ""friendly_id"": ""xyz-uvw-rst-opq"",
                    ""online"": true,
                    ""participants"": 3,
                    ""last_session"": null,
                    ""shared_owner"": ""Alice Example""
                }
            ],
            ""meta"": {}
        }";

        [Fact]
        public void Parses_Data_Envelope_And_Extracts_Rooms()
        {
            var token = JToken.Parse(RealisticBody);
            var array = (token as JObject)?["data"] as JArray;

            Assert.NotNull(array);
            Assert.Equal(2, array.Count);
            Assert.Equal("abc-def-ghi-jkl", array[0]["friendly_id"]?.ToString());
            Assert.Equal(42L, array[0]["id"]?.Value<long>());
            Assert.True(array[1]["online"]?.Value<bool>());
            Assert.Equal(3, array[1]["participants"]?.Value<int>());
        }

        [Fact]
        public void Shared_Owner_Is_Only_Present_For_Shared_Rooms()
        {
            var token = JToken.Parse(RealisticBody);
            var array = (JArray)((JObject)token)["data"];

            Assert.Null(array[0]["shared_owner"]);
            Assert.Equal("Alice Example", array[1]["shared_owner"]?.ToString());
        }

        [Fact]
        public void Unauthenticated_Response_Is_Errors_Envelope()
        {
            // Observed against meet.wald.rlp.de: 401 with body {"errors":[]}
            var body = "{\"errors\":[]}";
            var token = JToken.Parse(body);
            Assert.NotNull(token["errors"]);
            Assert.Null(token["data"]);
        }

        // --- ParseConfigurations ---

        [Fact]
        public void ParseConfigurations_extracts_data_object()
        {
            var json = "{\"data\":{\"record\":\"optional\",\"glRequireAuthentication\":\"default_enabled\"}}";
            var result = GreenlightClient.ParseConfigurations(json);
            Assert.Equal("optional", result["record"]);
            Assert.Equal("default_enabled", result["glRequireAuthentication"]);
        }

        [Fact]
        public void ParseConfigurations_returns_empty_for_empty_data()
        {
            var result = GreenlightClient.ParseConfigurations("{\"data\":{}}");
            Assert.Empty(result);
        }

        // --- ExtractFriendlyId ---

        [Theory]
        [InlineData("{\"data\":\"/rooms/abc-def-ghi\"}", "abc-def-ghi")]
        [InlineData("{\"data\":\"/rooms/xyz-uvw-123\"}", "xyz-uvw-123")]
        public void ExtractFriendlyId_parses_room_path(string json, string expected) =>
            Assert.Equal(expected, GreenlightClient.ExtractFriendlyId(json));

        [Fact]
        public void ExtractFriendlyId_throws_for_malformed_path()
        {
            Assert.Throws<System.InvalidOperationException>(
                () => GreenlightClient.ExtractFriendlyId("{\"data\":\"notapath\"}"));
        }
    }
}
