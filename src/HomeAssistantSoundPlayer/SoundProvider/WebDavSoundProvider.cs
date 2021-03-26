using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using WebDav;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    internal class WebDavSoundProvider : ISoundProvider
    {
        private readonly WebDavClient _client;
        private readonly ILogger<WebDavSoundProvider> _logger;
        private readonly string _path;

        public WebDavSoundProvider(ILogger<WebDavSoundProvider> logger, string host, string path, NetworkCredential credential)
        {
            _client = new WebDavClient(new WebDavClientParams
            {
                BaseAddress = new Uri(host),
                Credentials = credential
            });
            _logger = logger;
            _path = path;
        }

        public async Task<IList<string>> GetSounds()
        {
            _logger.LogInformation("Trying to get sound list from WebDAV...");

            var parameters = new PropfindParameters
            {
                Headers = new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("depth", "10")
                }
            };
            var result = await _client.Propfind(_path, parameters);
            if (!result.IsSuccessful)
            {
                throw new WebException("WebDav error " + result.StatusCode + " while listing directory");
            }

            var sounds = result.Resources.Where(x => !x.IsCollection).Select(x => x.Uri).ToList();

            _logger.LogInformation("Got {SoundCount} sounds!", sounds.Count);
            return sounds;
        }

        public async Task<byte[]> GetSound(string path)
        {
            var sw = Stopwatch.StartNew();
            var result = await _client.GetProcessedFile(path);
            if (!result.IsSuccessful)
            {
                throw new WebException($"WebDav error {result.StatusCode} while getting file {path}");
            }

            var ms = new MemoryStream();
            using (var webDavStream = result.Stream)
            {
                await webDavStream.CopyToAsync(ms);
                sw.Stop();
                _logger.LogInformation("Got sound {FilePath} in {ElapsedMilliseconds}ms", path, sw.ElapsedMilliseconds);
            }
            return ms.ToArray();
        }

        public Task Init(IList<string> sounds)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
