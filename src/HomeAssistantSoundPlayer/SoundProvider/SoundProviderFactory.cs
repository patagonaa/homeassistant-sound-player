using Microsoft.Extensions.Logging;
using System;
using System.Net;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    class SoundProviderFactory
    {
        private readonly ILoggerFactory _loggerFactory;

        public SoundProviderFactory(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory;
        }

        public ISoundProvider Get(string uriString)
        {
            var uri = new Uri(uriString);

            switch (uri.Scheme)
            {
                case "file":
                    return new FileSoundProvider(uri.AbsolutePath);
                case "http":
                case "https":
                    var creds = ParseUsernamePassword(uri.UserInfo);
                    return new WebDavSoundProvider(_loggerFactory.CreateLogger<WebDavSoundProvider>(), $"{uri.Scheme}://{uri.Authority}", uri.PathAndQuery, creds);
                default:
                    throw new ArgumentException($"Invalid URI Scheme: {uri.Scheme}");
            }
        }

        private NetworkCredential ParseUsernamePassword(string userPass)
        {
            var split = userPass.Split(':');
            return new NetworkCredential(split[0], split[1]);
        }
    }
}
