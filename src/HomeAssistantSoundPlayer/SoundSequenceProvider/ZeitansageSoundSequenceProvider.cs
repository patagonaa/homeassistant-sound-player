using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundSequenceProvider
{
    class ZeitansageSoundSequenceProvider : ISoundSequenceProvider
    {
        private IList<string> _hourSounds;
        private IList<string> _minuteSounds;
        private IList<string> _secondsSounds;
        private string _atNextSound = null;
        private string _beepSound = null;

        public void SetSounds(IEnumerable<string> sounds)
        {
            var soundsList = sounds.ToList();

            _minuteSounds = new List<string>();
            for (int i = 0; i <= 59; i++)
            {
                _minuteSounds.Add(soundsList.First(x => x.EndsWith($"{i:D3}.mp3")));
            }

            _secondsSounds = new List<string>();
            for (int i = 60; i <= 65; i++)
            {
                _secondsSounds.Add(soundsList.First(x => x.EndsWith($"{i:D3}.mp3")));
            }

            _hourSounds = new List<string>();
            for (int i = 100; i <= 123; i++)
            {
                _hourSounds.Add(soundsList.First(x => x.EndsWith($"{i:D3}.mp3")));
            }
            _atNextSound = soundsList.First(x => x.EndsWith("201BeimNaechstenTon.mp3"));
            _beepSound = soundsList.First(x => x.EndsWith("202Piep880Hz300ms.mp3"));
        }

        public async IAsyncEnumerable<string> GetNextSounds()
        {
            var speechBuffer = TimeSpan.FromSeconds(6); // estimated max length of speech before beep
            var nowWithBuffer = DateTime.Now + speechBuffer;
            var nowAligned = nowWithBuffer.AddSeconds(10 - (nowWithBuffer.Second % 10));

            yield return _atNextSound;
            yield return _hourSounds[nowAligned.Hour];
            yield return _minuteSounds[nowAligned.Minute];
            yield return _secondsSounds[nowAligned.Second / 10];

            var waitTime = nowAligned - DateTime.Now;
            if (waitTime > TimeSpan.Zero)
                await Task.Delay(waitTime);

            yield return _beepSound;
        }
    }
}
