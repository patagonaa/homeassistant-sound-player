using System.Collections.Generic;

namespace HomeAssistantSoundPlayer
{
    internal class Configuration
    {
        public string DeviceIdentifier { get; set; }
        public MqttConfig Mqtt { get; set; }
        public IList<SoundPoolConfig> SoundPools { get; set; }
    }
}
