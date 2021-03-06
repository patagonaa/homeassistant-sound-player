﻿using HomeAssistantSoundPlayer.SoundProvider;
using HomeAssistantSoundPlayer.SoundSequenceProvider;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Threading.Tasks;

namespace HomeAssistantSoundPlayer
{
    class Program
    {
        public static async Task Main(string[] args)
        {
            await Host.CreateDefaultBuilder(args)
                .ConfigureAppConfiguration(ConfigureAppConfiguration)
                .ConfigureServices(ConfigureServices)
                .ConfigureLogging(ConfigureLogging)
                .RunConsoleAsync();
        }

        private static void ConfigureLogging(HostBuilderContext ctx, ILoggingBuilder loggingBuilder)
        {
            loggingBuilder.ClearProviders();

            Log.Logger = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console()
                .CreateLogger();
            loggingBuilder.AddSerilog(Log.Logger);
        }

        private static void ConfigureServices(HostBuilderContext ctx, IServiceCollection services)
        {
            services.AddTransient<SoundProviderFactory>();
            services.AddTransient<SoundSequenceProviderFactory>();
            services.Configure<Configuration>(ctx.Configuration);
            services.AddHostedService<SoundPlayer>();
        }

        private static void ConfigureAppConfiguration(HostBuilderContext ctx, IConfigurationBuilder configBuilder)
        {
            configBuilder.Sources.Clear();
            configBuilder
                .AddJsonFile("configs/appSettings.json", optional: true)
                .AddEnvironmentVariables()
                .Build();
        }
    }
}
