using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer.SoundProvider
{
    class NormalizeSoundFilter : ISoundProvider
    {
        private readonly ILogger<NormalizeSoundFilter> _logger;
        private readonly ISoundProvider _source;

        public NormalizeSoundFilter(ILogger<NormalizeSoundFilter> logger, ISoundProvider source)
        {
            _logger = logger;
            _source = source;
        }

        public async Task<byte[]> GetSound(string path)
        {
            var sound = await _source.GetSound(path);
            var raw = await GetRawFromSound(sound);
            var normalizedRaw = Normalize(raw, out var clipped);
            if (clipped)
            {
                _logger.LogInformation("clipped {Path}!", path);
            }
            return await GetSoundFromRaw(normalizedRaw);
        }

        private byte[] Normalize(byte[] raw, out bool clipped)
        {
            var samples = new short[raw.Length / 2];

            if (!BitConverter.IsLittleEndian)
                throw new PlatformNotSupportedException("only little endian supported");

            for (int i = 0; i < samples.Length; i++)
            {
                samples[i] = BitConverter.ToInt16(raw, i * 2);
            }

            const double takePercentile = 0.995; // take the sample which is the x% loudest as a reference (0.99 with 1000 samples means "take the 10th loudest sample")
            const double targetGain = 0.5; // adjust amplitude (of the reference sample) to this level (0.5 = -6dB)

            var referenceAmplitude = samples
                .Select(x => x == short.MinValue ? short.MaxValue : Math.Abs(x)) // Abs(-32768) -> Exception
                .OrderBy(x => x) // sort by amplitude
                .Skip((int)(samples.Length * takePercentile)) // take the n%th sample
                .Select(x => (short?)x) // make nullable
                .FirstOrDefault();

            var factor = (short.MaxValue / (referenceAmplitude ?? short.MaxValue)) * targetGain;

            clipped = false;
            if (referenceAmplitude.HasValue)
            {
                for (int i = 0; i < samples.Length; i++)
                {
                    samples[i] = Clip(samples[i] * factor, out var sampleClipped);
                    clipped |= sampleClipped;
                }
            }

            var toReturn = new byte[raw.Length];

            for (int i = 0; i < samples.Length; i++)
            {
                Array.Copy(BitConverter.GetBytes(samples[i]), 0, toReturn, i * 2, 2);
            }

            return toReturn;
        }

        private short Clip(double? v, out bool clipped)
        {
            if (v > short.MaxValue)
            {
                clipped = true;
                return short.MaxValue;
            }
            if (v < short.MinValue)
            {
                clipped = true;
                return short.MinValue;
            }
            clipped = false;
            return (short)v;
        }

        private async Task<byte[]> GetRawFromSound(byte[] soundBytes)
        {
            return await RunFfmpeg("-i - -acodec pcm_s16le -f s16le -ac 2 -ar 44100 -", soundBytes);
        }

        private async Task<byte[]> GetSoundFromRaw(byte[] rawBytes)
        {
            return await RunFfmpeg("-acodec pcm_s16le -f s16le -ac 2 -ar 44100 -i - -f mp3 -", rawBytes);
        }

        private async Task<byte[]> RunFfmpeg(string args, byte[] input)
        {
            var startInfo = new ProcessStartInfo("ffmpeg", args)
            {
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true
            };

            var process = new Process()
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };

            process.ErrorDataReceived += (o, a) =>
            {
                if (a.Data == null)
                    return;

                _logger.LogDebug(a.Data);
            };

            process.Start();
            process.BeginErrorReadLine();

            using var msIn = new MemoryStream(input, false);
            using var msOut = new MemoryStream();
            await Task.WhenAll(msIn.CopyToAsync(process.StandardInput.BaseStream).ContinueWith(t => process.StandardInput.Close()), process.StandardOutput.BaseStream.CopyToAsync(msOut));
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new Exception($"FFMPEG processing failed! ffmpeg {args}");

            return msOut.ToArray();
        }

        public async Task<IList<string>> GetSounds()
        {
            return await _source.GetSounds();
        }

        public Task Init(IList<string> sounds)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
        }
    }
}
