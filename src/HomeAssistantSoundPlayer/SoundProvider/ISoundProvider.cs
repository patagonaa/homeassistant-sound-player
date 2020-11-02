using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    internal interface ISoundProvider : IDisposable
    {
        Task<Stream> GetSound(string path);
        IList<string> GetSounds();
    }
}
