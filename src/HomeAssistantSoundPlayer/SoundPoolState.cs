using HomeAssistantSoundPlayer.SoundProvider;
using HomeAssistantSoundPlayer.SoundSequenceProvider;
using System;

namespace HomeAssistantSoundPlayer
{
    internal class SoundPoolState : IDisposable
    {
        public string TopicBase { get; set; }
        public ISoundProvider SoundProvider { get; set; }
        public ISoundSequenceProvider SequenceProvider { get; set; }
        public SoundPoolConfig Config { get; set; }

        public void Dispose()
        {
            SoundProvider?.Dispose();
        }
    }
}