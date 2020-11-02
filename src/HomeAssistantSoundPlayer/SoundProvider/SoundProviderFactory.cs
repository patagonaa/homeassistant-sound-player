using System;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    class SoundProviderFactory
    {
        public ISoundProvider Get(string uriString)
        {
            var uri = new Uri(uriString);

            switch (uri.Scheme)
            {
                case "file":
                    return new FileSoundProvider(uri.AbsolutePath);
                default:
                    throw new ArgumentException($"Invalid URI Scheme: {uri.Scheme}");
            }
        }
    }
}
