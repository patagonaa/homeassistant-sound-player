using System;

namespace HomeAssistantSoundPlayer.SoundSequenceProvider
{
    class SoundSequenceProviderFactory
    {
        public ISoundSequenceProvider Get(string providerName)
        {
            switch (providerName)
            {
                case "QueueRandom":
                    return new QueueSoundRandomizer();
                case "Zeitansage":
                    return new ZeitansageSoundSequenceProvider();
                default:
                    throw new ArgumentException($"Invalid SequenceProviderName {providerName}");
            }
        }
    }
}
