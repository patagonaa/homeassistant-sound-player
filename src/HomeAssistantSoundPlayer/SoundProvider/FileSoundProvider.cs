using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    internal class FileSoundProvider : ISoundProvider
    {
        private readonly string _path;

        public FileSoundProvider(string path)
        {
            _path = path;
        }

        public Task<IList<string>> GetSounds()
        {
            return Task.FromResult<IList<string>>(Directory.GetFiles(_path, "*", SearchOption.AllDirectories));
        }

        public Task Init(IList<string> sounds)
        {
            return Task.CompletedTask;
        }

        public Task<byte[]> GetSound(string path)
        {
            return Task.FromResult<byte[]>(File.ReadAllBytes(path));
        }

        public void Dispose()
        {
        }
    }
}
