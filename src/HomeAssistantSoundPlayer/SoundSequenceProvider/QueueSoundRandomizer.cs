using System;
using System.Collections.Generic;
using System.Linq;

namespace HomeAssistantSoundPlayer.SoundSequenceProvider
{
    internal class QueueSoundRandomizer : ISoundSequenceProvider
    {
        private List<string> _allSounds;
        private Queue<string> _remainingSounds = new Queue<string>();
        private readonly Random _random;

        public QueueSoundRandomizer()
        {
            _random = new Random();
        }

        public void SetSounds(IEnumerable<string> sounds)
        {
            _allSounds = sounds.ToList();
            if (_allSounds.Count == 0)
                throw new InvalidOperationException("No Sounds available!");
            _remainingSounds.Clear();
        }

        private void Randomize()
        {
            _remainingSounds.Clear();
            foreach (var sound in _allSounds.OrderBy(x => _random.Next()))
            {
                _remainingSounds.Enqueue(sound);
            }
        }

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async IAsyncEnumerable<string> GetNextSounds()
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
        {
            if (_remainingSounds.Count == 0)
            {
                Randomize();
            }

            yield return _remainingSounds.Dequeue();
        }
    }
}
