using System;
using Newtonsoft.Json;

namespace OutlookGreenlight.AddIn.Models
{
    // Matches Greenlight v3 RoomSerializer: id, name, friendly_id, online, participants, last_session
    // plus conditional shared_owner. See bigbluebutton/greenlight app/serializers/room_serializer.rb.
    public class Room
    {
        [JsonProperty("id")]
        public long Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("friendly_id")]
        public string FriendlyId { get; set; }

        [JsonProperty("online")]
        public bool Online { get; set; }

        [JsonProperty("participants")]
        public int Participants { get; set; }

        [JsonProperty("last_session")]
        public DateTime? LastSession { get; set; }

        [JsonProperty("shared_owner")]
        public string SharedOwner { get; set; }

        [JsonIgnore]
        public string JoinUrl { get; set; }

        public override string ToString() => Name ?? FriendlyId ?? Id.ToString();
    }
}
