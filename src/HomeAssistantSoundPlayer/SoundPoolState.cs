namespace HomeAssistantSoundPlayer
{
    internal class SoundPoolState
    {
        public string TopicBase { get; set; }
        public int VolumePercent { get; set; }
        public SoundProvider SoundProvider { get; set; }
        public SoundPoolConfig Config { get; set; }
    }
}