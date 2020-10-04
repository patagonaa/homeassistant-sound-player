using Microsoft.Extensions.Configuration;
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
    class Program
    {
        private static IList<SoundPoolConfig> _soundPools;
        private static IManagedMqttClient _mqttClient;
        private static Configuration _config;
        private static readonly Random _random = new Random();

        static async Task Main(string[] args)
        {
            var configRoot = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();

            _config = configRoot.Get<Configuration>();
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

            _soundPools = _config.SoundPools;

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

            await Task.Delay(Timeout.Infinite);
        }

        private static async Task MessageReceived(MqttApplicationMessageReceivedEventArgs messageEvent)
        {
            var message = messageEvent.ApplicationMessage;
            var splitTopic = message.Topic.Split('/');
            var payload = message.ConvertPayloadToString();
            var soundPoolId = splitTopic[3];

            Console.WriteLine($"{message.Topic} {payload}");

            var soundPool = _soundPools.SingleOrDefault(x => x.Identifier == soundPoolId);

            var state = payload.Equals("ON", StringComparison.OrdinalIgnoreCase);

            if (!state)
                return;

            Console.WriteLine($"Starting sound {soundPool}");

            await PlaySoundFromPool(soundPool);
        }

        private static async Task PlaySoundFromPool(SoundPoolConfig soundPool)
        {
            var files = Directory.GetFiles(soundPool.Directory, "*", SearchOption.AllDirectories);
            var randomSound = files[_random.Next(files.Length)];

            try
            {
                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/state", "ON", MqttQualityOfServiceLevel.ExactlyOnce, true);
                var retries = 3;
                for (int i = 1; i <= retries; i++)
                {
                    try
                    {
                        PlaySound(randomSound);
                        break;
                    }
                    catch (Exception)
                    {
                        if (i == retries)
                            throw;
                        Console.WriteLine("PlaySound failed! Retrying!");
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("PlaySound failed! " + ex.ToString());
            }
            finally
            {
                await _mqttClient.PublishAsync($"homeassistant/switch/{_config.DeviceIdentifier}_sounds/{soundPool.Identifier}/state", "OFF", MqttQualityOfServiceLevel.ExactlyOnce, true);
            }
        }

        private static void PlaySound(string soundFile)
        {
            var process = new Process();

            var startInfo = new ProcessStartInfo("ffplay");
            startInfo.ArgumentList.Add("-i");
            startInfo.ArgumentList.Add(soundFile);
            startInfo.ArgumentList.Add("-nodisp");
            startInfo.ArgumentList.Add("-autoexit");

            process.StartInfo = startInfo;

            process.ErrorDataReceived += (o, a) => Console.WriteLine(a.Data);

            Console.WriteLine($"Playing Sound {soundFile}");

            process.Start();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception($"Error while playing sound! ExitCode {process.ExitCode}");
            }

            Console.WriteLine($"Done! ExitCode {process.ExitCode}");
        }
    }
}
