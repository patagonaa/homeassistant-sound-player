using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    internal interface ISoundProvider : IDisposable
    {
        Task<IList<string>> GetSounds();
        Task<byte[]> GetSound(string path);
        /// <summary>
        /// This can be called multiple times, e.g. when the provider should repopulate its caches
        /// </summary>
        Task Init(IList<string> sounds);
    }
}
