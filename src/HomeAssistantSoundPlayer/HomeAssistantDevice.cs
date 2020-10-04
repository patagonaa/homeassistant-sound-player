using Newtonsoft.Json;
using System.Collections.Generic;

namespace HomeAssistantSoundPlayer
{
    internal class HomeAssistantDevice
    {
        [JsonProperty("identifiers")]
        public IList<string> Identifiers { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
    }
}
