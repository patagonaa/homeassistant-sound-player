using Newtonsoft.Json;

namespace HomeAssistantSoundPlayer
{
    internal class HomeAssistantDiscovery
    {
        [JsonProperty("unique_id")]
        public string UniqueId { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("~")]
        public string TopicBase { get; set; }
        [JsonProperty("command_topic")]
        public string CommandTopic { get; set; }
        [JsonProperty("availability_topic")]
        public string AvailabilityTopic { get; set; }
        [JsonProperty("state_topic")]
        public string StateTopic { get; set; }
        [JsonProperty("retain")]
        public bool Retain { get; set; }
        [JsonProperty("device")]
        public HomeAssistantDevice Device { get; set; }
    }
}
