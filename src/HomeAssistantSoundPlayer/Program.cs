using Microsoft.Extensions.Configuration;
using System.Threading;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var configRoot = new ConfigurationBuilder()
                .AddJsonFile("config.json")
                .Build();

            var config = configRoot.Get<Configuration>();

            var player = new SoundPlayer(config);

            await player.Start();

            await Task.Delay(Timeout.Infinite);
        }
    }
}
