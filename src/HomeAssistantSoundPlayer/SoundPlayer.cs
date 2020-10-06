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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer
{
    internal class SoundPlayer : IHostedService
    {
        private readonly Configuration _config;
        private IManagedMqttClient _mqttClient;
        private readonly IDictionary<string, SoundProvider> _soundProviders = new Dictionary<string, SoundProvider>();
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger<SoundPlayer> _logger;

        public SoundPlayer(IOptions<Configuration> config, ILoggerFactory loggerFactory)
        {
            _config = config.Value;
            _loggerFactory = loggerFactory;
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
            _logger.LogInformation("Started!");
        }

        public async Task StopAsync(CancellationToken token)
        {
            _logger.LogInformation("Stopping...");
            await _mqttClient?.StopAsync();
            _mqttClient?.Dispose();
            foreach (var provider in _soundProviders.Values)
            {
                provider.Dispose();
            }
            _logger.LogInformation("Stopped!");
        }

        private async Task SetupHomeAssistantAutoDiscovery()
        {
            foreach (var soundPool in _config.SoundPools)
            {
                var switchConfig = new HomeAssistantDiscovery()
                {
                    UniqueId = $"{_config.DeviceIdentifier}_soundpool_{soundPool.Identifier}",
                    Name = $"SoundPool {soundPool.Name}",
                    TopicBase = $"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}",
                    CommandTopic = "~/set",
                    StateTopic = "~/state",
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

                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/config", configJson, MqttQualityOfServiceLevel.ExactlyOnce, true);
                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
                await _mqttClient.SubscribeAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/set");
            }
        }

        private async Task MessageReceived(MqttApplicationMessageReceivedEventArgs messageEvent)
        {
            var message = messageEvent.ApplicationMessage;
            var splitTopic = message.Topic.Split('/');
            var payload = message.ConvertPayloadToString();
            var soundPoolId = splitTopic[3];

            _logger.LogDebug("{MessageTopic} {MessagePayload}", message.Topic, payload);

            var soundPool = _config.SoundPools.SingleOrDefault(x => x.Identifier == soundPoolId);

            var state = payload.Equals("ON", StringComparison.OrdinalIgnoreCase);

            if (!state)
                return;

            _logger.LogInformation("Starting sound from pool {SoundPool}", soundPool.Name);

            await PlaySoundFromPool(soundPool);
        }

        private async Task PlaySoundFromPool(SoundPoolConfig soundPool)
        {
            SoundProvider soundProvider;
            if (!_soundProviders.TryGetValue(soundPool.Identifier, out soundProvider))
            {
                _soundProviders[soundPool.Identifier] = soundProvider = new SoundProvider(soundPool, _loggerFactory.CreateLogger<SoundProvider>());
            }

            try
            {
                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/state", "ON", MqttQualityOfServiceLevel.ExactlyOnce, true);

                var nextSound = soundProvider.GetNextSound();
                var retries = 3;
                for (int i = 1; i <= retries; i++)
                {
                    try
                    {
                        using (var sound = await soundProvider.GetSound(nextSound))
                        {
                            await PlaySound(sound);
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
                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
            }
        }

        private async Task PlaySound(Stream soundFile)
        {
            var tcs = new TaskCompletionSource<bool>();

            var startInfo = new ProcessStartInfo("ffplay", "-i - -nodisp -autoexit")
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
    }
}
