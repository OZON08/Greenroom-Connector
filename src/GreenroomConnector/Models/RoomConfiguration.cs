using System.Collections.Generic;

namespace GreenroomConnector.Models
{
    public class RoomConfiguration
    {
        private readonly Dictionary<string, string> _config;

        public RoomConfiguration(Dictionary<string, string> config)
        {
            _config = config ?? new Dictionary<string, string>();
        }

        public bool CanChange(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "optional" || v == "default_enabled";
        }

        public bool IsDefaultEnabled(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "default_enabled";
        }

        public bool CanToggleAccessCode(string settingName)
        {
            _config.TryGetValue(settingName, out var v);
            return v == "optional" || v == "default_enabled" || v == "true";
        }
    }
}
