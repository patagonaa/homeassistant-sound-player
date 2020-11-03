﻿using HomeAssistantDiscoveryHelper;
using HomeAssistantSoundPlayer.SoundProvider;
using HomeAssistantSoundPlayer.SoundRandomizer;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MQTTnet;
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
        private IManagedMqttClient _mqttClient;
        private Task _checkNewSoundsTask;
        private readonly IDictionary<string, SoundPoolState> _soundPools = new Dictionary<string, SoundPoolState>();
        private readonly SoundProviderFactory _soundProviderFactory;
        private readonly ILogger<SoundPlayer> _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        public SoundPlayer(IOptions<Configuration> config, ILoggerFactory loggerFactory, SoundProviderFactory soundProviderFactory)
        {
            _config = config.Value;
            _soundProviderFactory = soundProviderFactory;
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

            await _mqttClient.StartAsync(options);
            await _mqttClient.PublishAsync($"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state", "online", MqttQualityOfServiceLevel.ExactlyOnce, true);

            await SetupHomeAssistantAutoDiscovery();
            await SetupSoundPools();

            _checkNewSoundsTask = Task.Run(() => CheckForNewSounds());
            _logger.LogInformation("Started!");
        }

        public async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("Stopping...");
            _cts.Cancel();
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

        private async Task SetupHomeAssistantAutoDiscovery()
        {
            foreach (var soundPoolConfig in _config.SoundPools)
            {
                var topicBase = $"homeassistant/light/{_config.DeviceIdentifier}_sounds/{soundPoolConfig.Identifier}";

                var switchConfig = new HomeAssistantDiscovery()
                {
                    UniqueId = $"{_config.DeviceIdentifier}_soundpool_{soundPoolConfig.Identifier}",
                    Name = $"SoundPool {soundPoolConfig.Name}",
                    TopicBase = topicBase,

                    CommandTopic = "~/play",
                    StateTopic = "~/play_state",

                    BrightnessCommandTopic = "~/volume",
                    BrightnessStateTopic = "~/volume_state",
                    BrightnessScale = 100,

                    Device = new HomeAssistantDevice
                    {
                        Name = _config.DeviceIdentifier,
                        Identifiers = new List<string>
                        {
                            _config.DeviceIdentifier
                        }
                    },
                    AvailabilityTopic = $"HomeAssistantSoundPlayer/{_config.DeviceIdentifier}/state",
                    Retain = false
                };

                var configJson = JsonConvert.SerializeObject(switchConfig);

                await _mqttClient.PublishAsync($"{topicBase}/config", configJson, MqttQualityOfServiceLevel.ExactlyOnce, true);
                await _mqttClient.PublishAsync($"{topicBase}/play_state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
                await _mqttClient.SubscribeAsync($"{topicBase}/play");
                await _mqttClient.SubscribeAsync($"{topicBase}/volume");
            }
        }

        private async Task SetupSoundPools()
        {
            foreach (var soundPoolConfig in _config.SoundPools)
            {
                var topicBase = $"homeassistant/light/{_config.DeviceIdentifier}_sounds/{soundPoolConfig.Identifier}";

                var soundProvider = _soundProviderFactory.Get(soundPoolConfig.Uri);
                var randomizer = new QueueSoundRandomizer();
                var sounds = await soundProvider.GetSounds();
                randomizer.SetSounds(sounds);
                await soundProvider.PopulateCache(sounds);

                _soundPools[soundPoolConfig.Identifier] = new SoundPoolState
                {
                    TopicBase = topicBase,
                    VolumePercent = 100,
                    Config = soundPoolConfig,
                    Randomizer = randomizer,
                    SoundProvider = soundProvider
                };
            }
        }

        private async Task MessageReceived(MqttApplicationMessageReceivedEventArgs messageEvent)
        {
            var message = messageEvent.ApplicationMessage;
            var payload = message.ConvertPayloadToString();

            _logger.LogInformation("{MessageTopic} {MessagePayload}", message.Topic, payload);

            var splitTopic = message.Topic.Split('/');

            var soundPoolId = splitTopic[3];
            var command = splitTopic[4];

            var soundPoolState = _soundPools[soundPoolId];

            switch (command)
            {
                case "play":
                    var play = payload.Equals("ON", StringComparison.OrdinalIgnoreCase);

                    if (!play)
                        return;

                    _logger.LogInformation("Starting sound from pool {SoundPool}", soundPoolState.Config.Name);

                    await PlaySoundFromPool(soundPoolState);
                    break;
                case "volume":
                    var volume = int.Parse(payload, CultureInfo.InvariantCulture);
                    soundPoolState.VolumePercent = volume;
                    await _mqttClient.PublishAsync($"{soundPoolState.TopicBase}/volume_state", volume.ToString(CultureInfo.InvariantCulture), MqttQualityOfServiceLevel.ExactlyOnce, true);
                    break;
                default:
                    break;
            }
        }

        private async Task PlaySoundFromPool(SoundPoolState soundPool)
        {
            var soundProvider = soundPool.SoundProvider;
            var randomizer = soundPool.Randomizer;

            try
            {
                await _mqttClient.PublishAsync($"{soundPool.TopicBase}/play_state", "ON", MqttQualityOfServiceLevel.ExactlyOnce, true);

                var nextSound = randomizer.GetNextSound();

                var retries = 3;
                for (int i = 1; i <= retries; i++)
                {
                    try
                    {
                        using (var sound = await soundProvider.GetSound(nextSound))
                        {
                            await PlaySound(sound, soundPool.VolumePercent);
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
                await _mqttClient.PublishAsync($"{soundPool.TopicBase}/play_state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
            }
        }

        private async Task PlaySound(Stream soundFile, int volumePercent)
        {
            var tcs = new TaskCompletionSource<bool>();

            var startInfo = new ProcessStartInfo("ffplay", $"-volume {volumePercent.ToString(CultureInfo.InvariantCulture)} -i - -nodisp -autoexit")
            {
                UseShellExecute = false,
                RedirectStandardInput = true,
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

            using (var stdin = process.StandardInput.BaseStream)
            {
                await soundFile.CopyToAsync(stdin);
            }

            await tcs.Task;
        }

        private async Task CheckForNewSounds()
        {
            while (!_cts.Token.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromMinutes(10), _cts.Token);

                foreach (var soundPool in _soundPools.Values)
                {
                    if (soundPool.SoundProvider == null || soundPool.Randomizer == null || _cts.Token.IsCancellationRequested)
                        continue;

                    var sounds = await soundPool.SoundProvider.GetSounds();
                    await soundPool.SoundProvider.PopulateCache(sounds);
                    soundPool.Randomizer.SetSounds(sounds);
                }
            }
        }
    }
}
