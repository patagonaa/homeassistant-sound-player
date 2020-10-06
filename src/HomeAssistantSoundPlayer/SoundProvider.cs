using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer
{
    internal class SoundProvider : IDisposable
    {
        private readonly Random _random = new Random();
        private SoundPoolConfig _config;
        private readonly ILogger<SoundProvider> _logger;
        private readonly Queue<string> _remainingSounds = new Queue<string>();

        public SoundProvider(SoundPoolConfig config, ILogger<SoundProvider> logger)
        {
            _config = config;
            _logger = logger;
        }

        private IList<string> GetSounds()
        {
            return Directory.GetFiles(_config.Directory, "*", SearchOption.AllDirectories);
        }

        public string GetNextSound()
        {
            if(_remainingSounds.Count == 0)
            {
                var allSounds = GetSounds();
                if (allSounds.Count == 0)
                    throw new InvalidOperationException("No Sounds available!");
                foreach (var sound in allSounds.OrderBy(x => _random.Next()))
                {
                    _remainingSounds.Enqueue(sound);
                }
            }

            var randomSound = _remainingSounds.Dequeue();
            _logger.LogInformation("Random sound is... {SoundName}", randomSound);
            return randomSound;
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
