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

        public Task PopulateCache(IEnumerable<string> sounds)
        {
            return Task.CompletedTask;
        }

        public Task<Stream> GetSound(string path)
        {
            return Task.FromResult<Stream>(File.OpenRead(path));
        }

        public void Dispose()
        {
        }
    }
}
