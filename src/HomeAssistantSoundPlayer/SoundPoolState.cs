using HomeAssistantSoundPlayer.SoundProvider;
using HomeAssistantSoundPlayer.SoundRandomizer;
using System;

namespace HomeAssistantSoundPlayer
{
    internal class SoundPoolState : IDisposable
    {
        public string TopicBase { get; set; }
        public int VolumePercent { get; set; }
        public ISoundProvider SoundProvider { get; set; }
        public ISoundRandomizer Randomizer { get; set; }
        public SoundPoolConfig Config { get; set; }

        public void Dispose()
        {
            SoundProvider?.Dispose();
        }
    }
}