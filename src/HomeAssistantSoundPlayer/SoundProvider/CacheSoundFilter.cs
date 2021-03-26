using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    class CacheSoundFilter : ISoundProvider
    {
        private readonly ConcurrentDictionary<string, byte[]> _cache = new ConcurrentDictionary<string, byte[]>();
        private readonly ILogger<CacheSoundFilter> _logger;
        private readonly ISoundProvider _source;

        public CacheSoundFilter(ILogger<CacheSoundFilter> logger, ISoundProvider source)
        {
            _logger = logger;
            _source = source;
        }
        public async Task Init(IList<string> sounds)
        {
            await _source.Init(sounds);
            foreach (var sound in sounds)
            {
                if (!_cache.ContainsKey(sound))
                {
                    try
                    {
                        _cache[sound] = await _source.GetSound(sound);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error while getting sound {SoundName}", sound);
                    }
                }
            }
        }

        public async Task<byte[]> GetSound(string path)
        {
            if (!_cache.TryGetValue(path, out var bytes))
            {
                _logger.LogWarning("Cache miss! This should not happen if everything's initialized correctly!");
                _cache[path] = bytes = await _source.GetSound(path);
            }

            return bytes;
        }

        public async Task<IList<string>> GetSounds()
        {
            return await _source.GetSounds();
        }

        public void Dispose()
        {
            _source.Dispose();
            _cache.Clear();
        }
    }
}
