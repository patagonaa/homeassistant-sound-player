using System.Collections.Generic;

namespace HomeAssistantSoundPlayer.SoundSequenceProvider
{
    internal interface ISoundSequenceProvider
    {
        void SetSounds(IEnumerable<string> sounds);
        IAsyncEnumerable<string> GetNextSounds();
    }
}
