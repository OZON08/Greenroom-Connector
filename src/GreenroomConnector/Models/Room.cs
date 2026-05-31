using System;
using Newtonsoft.Json;

namespace GreenroomConnector.Models
{
    // Matches Greenlight v3 RoomSerializer: id, name, friendly_id, online, participants, last_session
    // plus conditional shared_owner. See bigbluebutton/greenlight app/serializers/room_serializer.rb.
    public class Room
    {
        // Greenlight v3 uses UUID strings as primary keys.
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("friendly_id")]
        public string FriendlyId { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }

        [JsonProperty("participants")]
        public int? Participants { get; set; }

        [JsonProperty("last_session")]
        public DateTime? LastSession { get; set; }

        [JsonProperty("shared_owner")]
        public string SharedOwner { get; set; }

        [JsonIgnore]
        public string JoinUrl { get; set; }

        // Viewer access code to print in the appointment body (set by the picker
        // when the selected room has one). Null/empty means no code line.
        [JsonIgnore]
        public string AccessCode { get; set; }

        // True when the room was inserted via "Als Moderator einfügen" — drives
        // the moderator-access note in the appointment body.
        [JsonIgnore]
        public bool IsModeratorLink { get; set; }

        public override string ToString() => Name ?? FriendlyId ?? Id.ToString();
    }
}
