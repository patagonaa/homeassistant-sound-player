using HomeAssistantDiscoveryHelper;
using HomeAssistantSoundPlayer.SoundProvider;
using HomeAssistantSoundPlayer.SoundSequenceProvider;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
using MQTTnet.Client.Connecting;
using MQTTnet.Client.Options;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet.Protocol;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer
{
    internal class SoundPlayer : IHostedService
    {
        private readonly Configuration _config;
        private readonly IDictionary<string, SoundPoolState> _soundPools = new Dictionary<string, SoundPoolState>();
        private readonly SoundProviderFactory _soundProviderFactory;
        private readonly SoundSequenceProviderFactory _soundSequenceProviderFactory;
        private readonly ILogger<SoundPlayer> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private IManagedMqttClient _mqttClient;
        private Task _checkNewSoundsTask;
        private bool _volumeMuted;
        private int _volumePercent;
        public SoundPlayer(IOptions<Configuration> config, ILoggerFactory loggerFactory, SoundProviderFactory soundProviderFactory, SoundSequenceProviderFactory soundSequenceProviderFactory)
        {
            _config = config.Value;
            _soundProviderFactory = soundProviderFactory;
            _soundSequenceProviderFactory = soundSequenceProviderFactory;
            _logger = loggerFactory.CreateLogger<SoundPlayer>();
        }

        public async Task StartAsync(CancellationToken token)
        {
            _logger.LogInformation("Starting...");
            var mqttConfig = _config.Mqtt;

            var options = new ManagedMqttClientOptionsBuilder()
                .WithAutoReconnectDelay(TimeSpan.FromSeconds(5))
                .WithClientOptions(new MqttClientOptionsBuilder()
                    .WithTcpServer(mqttConfig.Host)
                    .WithCredentials(mqttConfig.Username, mqttConfig.Password)
                    .WithWillMessage(new MqttApplicationMessage { Topic = $"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state", Payload = Encoding.UTF8.GetBytes("offline") })
                    .Build())
                .Build();

            _mqttClient = new MqttFactory().CreateManagedMqttClient();

            _mqttClient.UseApplicationMessageReceivedHandler(MessageReceived);

            _mqttClient.UseConnectedHandler(OnConnected);

            await _mqttClient.StartAsync(options);
            await SetupSoundPools();
            await SetupVolumeControl();

            _checkNewSoundsTask = Task.Run(() => CheckForNewSounds());
            _logger.LogInformation("Started!");
        }
        public async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("Stopping...");
            _cts.Cancel();
            if (_mqttClient?.IsConnected ?? false)
                await _mqttClient?.PublishAsync($"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state", "offline", MqttQualityOfServiceLevel.ExactlyOnce, true);
            await _mqttClient?.StopAsync();
            _mqttClient?.Dispose();
            if (_checkNewSoundsTask != null)
                await _checkNewSoundsTask;
            foreach (var provider in _soundPools.Values)
            {
                provider.Dispose();
            }
            _logger.LogInformation("Stopped!");
        }

        private async Task OnConnected(MqttClientConnectedEventArgs args)
        {
            await SetupHomeAssistantAutoDiscovery();
            await _mqttClient.PublishAsync($"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state", "online", MqttQualityOfServiceLevel.ExactlyOnce, true);
        }

        private async Task SetupHomeAssistantAutoDiscovery()
        {
            var device = new HomeAssistantDevice
            {
                Name = _config.DeviceIdentifier,
                Identifiers = new List<string>
                {
                    _config.DeviceIdentifier
                }
            };

            foreach (var soundPoolConfig in _config.SoundPools)
            {
                var switchTopicBase = $"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPoolConfig.Identifier}";

                var switchConfig = new HomeAssistantDiscovery()
                {
                    UniqueId = $"{_config.DeviceIdentifier}_soundpool_{soundPoolConfig.Identifier}",
                    Name = $"SoundPool {soundPoolConfig.Name}",
                    TopicBase = switchTopicBase,
                    CommandTopic = "~/set",
                    StateTopic = "~/state",
                    Device = device,
                    AvailabilityTopic = $"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state",
                    Retain = false
                };

                var configJson = JsonConvert.SerializeObject(switchConfig);

                await _mqttClient.PublishAsync($"{switchTopicBase}/config", configJson, MqttQualityOfServiceLevel.ExactlyOnce, true);
                await _mqttClient.PublishAsync($"{switchTopicBase}/state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
            }

            var lightTopicBase = $"homeassistant/light/{_config.DeviceIdentifier}_sounds/volume";
            var volumeLightConfig = new HomeAssistantDiscovery
            {
                UniqueId = $"{_config.DeviceIdentifier}_volume",
                Name = $"Volume",
                TopicBase = lightTopicBase,

                CommandTopic = "~/set",
                StateTopic = "~/state",

                BrightnessCommandTopic = "~/volume_set",
                BrightnessStateTopic = "~/volume_state",
                BrightnessScale = 100,

                Device = device,
                AvailabilityTopic = $"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state",
                Retain = true
            };

            var volumeConfigJson = JsonConvert.SerializeObject(volumeLightConfig);

            await _mqttClient.PublishAsync($"{lightTopicBase}/config", volumeConfigJson, MqttQualityOfServiceLevel.ExactlyOnce, true);
        }

        private async Task SetupSoundPools()
        {
            foreach (var soundPoolConfig in _config.SoundPools)
            {
                var topicBase = $"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPoolConfig.Identifier}";

                var soundProvider = _soundProviderFactory.Get(soundPoolConfig.Uri);
                var sequenceProvider = _soundSequenceProviderFactory.Get(soundPoolConfig.SequenceProvider);
                var sounds = await soundProvider.GetSounds();
                sequenceProvider.SetSounds(sounds);
                await soundProvider.Init(sounds);

                _soundPools[soundPoolConfig.Identifier] = new SoundPoolState
                {
                    TopicBase = topicBase,
                    Config = soundPoolConfig,
                    SequenceProvider = sequenceProvider,
                    SoundProvider = soundProvider
                };
                await _mqttClient.SubscribeAsync($"{topicBase}/set");
            }
        }

        private async Task SetupVolumeControl()
        {
            var lightTopicBase = $"homeassistant/light/{_config.DeviceIdentifier}_sounds/volume";
            await _mqttClient.SubscribeAsync($"{lightTopicBase}/set");
            await _mqttClient.SubscribeAsync($"{lightTopicBase}/volume_set");
        }

        private async Task MessageReceived(MqttApplicationMessageReceivedEventArgs messageEvent)
        {
            var message = messageEvent.ApplicationMessage;
            var payload = message.ConvertPayloadToString();

            _logger.LogInformation("{MessageTopic} {MessagePayload}", message.Topic, payload);

            var splitTopic = message.Topic.Split('/');

            var soundPoolId = splitTopic[3];
            var command = splitTopic[4];

            if(soundPoolId == "volume")
            {
                var lightTopicBase = $"homeassistant/light/{_config.DeviceIdentifier}_sounds/volume";

                switch (command)
                {
                    case "set":
                        var isOn = payload.Equals("ON", StringComparison.OrdinalIgnoreCase);
                        _volumeMuted = !isOn;
                        await _mqttClient.PublishAsync($"{lightTopicBase}/state", isOn ? "ON" : "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
                        _logger.LogInformation("Mute state set to {MuteState}", _volumeMuted);
                        break;
                    case "volume_set":
                        var volume = int.Parse(payload, CultureInfo.InvariantCulture);
                        _volumePercent = volume;
                        await _mqttClient.PublishAsync($"{lightTopicBase}/volume_state", volume.ToString(CultureInfo.InvariantCulture), MqttQualityOfServiceLevel.ExactlyOnce, true);
                        _logger.LogInformation("Volume set to {VolumeState}", _volumePercent);
                        break;
                    default:
                        break;
                }
            }
            else
            {
                var soundPoolState = _soundPools[soundPoolId];

                switch (command)
                {
                    case "set":
                        var play = payload.Equals("ON", StringComparison.OrdinalIgnoreCase);

                        if (!play)
                            return;

                        _logger.LogInformation("Starting sound from pool {SoundPool}", soundPoolState.Config.Name);

                        await PlaySoundFromPool(soundPoolState);
                        break;
                    default:
                        break;
                }
            }
        }

        private async Task PlaySoundFromPool(SoundPoolState soundPool)
        {
            var soundProvider = soundPool.SoundProvider;
            var randomizer = soundPool.SequenceProvider;

            try
            {
                await _mqttClient.PublishAsync($"{soundPool.TopicBase}/state", "ON", MqttQualityOfServiceLevel.ExactlyOnce, true);

                var nextSounds = randomizer.GetNextSounds();

                var retries = 3;
                for (int i = 1; i <= retries; i++)
                {
                    try
                    {
                        await foreach (var nextSound in nextSounds)
                        {
                            _logger.LogInformation("Playing sound {SoundName}", nextSound);

                            var sw = Stopwatch.StartNew();
                            var sound = await soundProvider.GetSound(nextSound);
                            sw.Stop();
                            _logger.LogInformation("GetSound took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

                            await PlaySound(sound, _volumeMuted ? 0 : _volumePercent);
                        }

                        break;
                    }
                    catch (Exception)
                    {
                        if (i == retries)
                            throw;
                        _logger.LogWarning("PlaySound failed! Retrying!");
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "PlaySound failed! ");
            }
            finally
            {
                await _mqttClient.PublishAsync($"{soundPool.TopicBase}/state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
            }
        }

        private async Task PlaySound(byte[] soundFile, int volumePercent)
        {
            var tcs = new TaskCompletionSource<bool>();

            var sw = Stopwatch.StartNew();
            var tmpFileName = Path.GetTempFileName();
            await File.WriteAllBytesAsync(tmpFileName, soundFile);
            sw.Stop();
            _logger.LogInformation("Copy to tmp file took {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);

            var startInfo = new ProcessStartInfo("ffplay", $"-volume {volumePercent.ToString(CultureInfo.InvariantCulture)} -i \"{tmpFileName}\" -nodisp -autoexit")
            {
                UseShellExecute = false,
                RedirectStandardError = true
            };

            using var process = new Process
            {
                StartInfo = startInfo,
                EnableRaisingEvents = true
            };
            bool errorInLog = false;

            process.ErrorDataReceived += (o, a) =>
            {
                if (a.Data == null)
                    return;

                _logger.LogInformation(a.Data);
                if (a.Data.Contains("open failed") || a.Data.Contains("Failed to open file"))
                {
                    errorInLog = true;
                }
            };
            process.Exited += (o, a) =>
            {
                if (process.ExitCode != 0)
                {
                    tcs.SetException(new Exception($"Error while playing sound! ExitCode {process.ExitCode}"));
                    return;
                }
                if (errorInLog)
                {
                    tcs.SetException(new Exception("Error while playing sound! ffplay logged an error!"));
                    return;
                }
                _logger.LogInformation($"Done!");
                tcs.SetResult(true);
            };
            process.Start();
            process.BeginErrorReadLine();

            await tcs.Task;

            try
            {
                File.Delete(tmpFileName);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting Temp File {FileName}. Too bad!", tmpFileName);
            }
        }

        private async Task CheckForNewSounds()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(10), _cts.Token);
                    _logger.LogInformation("Checking for new sounds");
                    foreach (var soundPool in _soundPools.Values)
                    {
                        if (soundPool.SoundProvider == null || soundPool.SequenceProvider == null || _cts.Token.IsCancellationRequested)
                            continue;

                        var sounds = await soundPool.SoundProvider.GetSounds();
                        await soundPool.SoundProvider.Init(sounds);
                        soundPool.SequenceProvider.SetSounds(sounds);
                    }
                }
                catch (TaskCanceledException)
                {
                }
            }
        }
    }
}
