namespace HomeAssistantSoundPlayer
{
    internal class SoundPoolConfig
    {
        public string Name { get; set; }
        public string Identifier { get; set; }
        public string Uri { get; set; }
        public string SequenceProvider { get; set; } = "QueueRandom";
    }
}
