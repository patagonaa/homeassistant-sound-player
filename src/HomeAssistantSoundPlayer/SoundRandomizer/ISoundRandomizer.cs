using System.Collections.Generic;

namespace HomeAssistantSoundPlayer.SoundRandomizer
{
    internal interface ISoundRandomizer
    {
        void SetSounds(IEnumerable<string> sounds);
        string GetNextSound();
    }
}
