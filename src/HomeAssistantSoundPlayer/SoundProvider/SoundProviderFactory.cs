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
            var splitUri = uriString.Split(':');
            if (splitUri.Length < 2)
                throw new ArgumentException("Uri must contain protocol");

            switch (splitUri[0])
            {
                case "file":
                    return new FileSoundProvider(uriString.Replace("file://", ""));
                case "http":
                case "https":
                    var uri = new Uri(uriString);
                    var creds = ParseUsernamePassword(uri.UserInfo);
                    return new WebDavSoundProvider(_loggerFactory.CreateLogger<WebDavSoundProvider>(), $"{uri.Scheme}://{uri.Authority}", uri.PathAndQuery, creds);
                default:
                    throw new ArgumentException($"Invalid URI Scheme: {splitUri[0]}");
            }
        }

        private NetworkCredential ParseUsernamePassword(string userPass)
        {
            var split = userPass.Split(':');
            return new NetworkCredential(split[0], split[1]);
        }
    }
}
