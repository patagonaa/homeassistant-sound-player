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

        public IList<string> GetSounds()
        {
            return Directory.GetFiles(_path, "*", SearchOption.AllDirectories);
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
