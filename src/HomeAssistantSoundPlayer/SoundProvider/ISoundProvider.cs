using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    internal interface ISoundProvider : IDisposable
    {
        Task<IList<string>> GetSounds();
        Task PopulateCache(IEnumerable<string> sounds);
        Task<Stream> GetSound(string path);
    }
}
