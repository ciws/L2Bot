using Client.Domain.Factories;
using Client.Infrastructure.Factories;
using Client.Domain.Parsers;
using Client.Infrastructure.Parsers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Windows;
using Client.Domain.Transports;
using Client.Infrastructure.Transports;
using Client.Domain.Entities;
using Client.Domain.Service;
using Client.Domain.ValueObjects;
using Microsoft.Extensions.Configuration;
using System.Reflection;
using Client.Application.Views;
using Client.Application.ViewModels;
using Client.Domain.Helpers;
using Client.Infrastructure.Helpers;
using Client.Domain.Events;
using Client.Infrastructure.Events;
using System;
using Client.Infrastructure.Service;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Client
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : System.Windows.Application
    {
        public static IHost? AppHost { get; private set; }
        private Bot? bot;

        public App()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices(
                    (hostContext, services) => ConfigureServices(services)
                )
                .Build();
        }

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                ShutdownMode = ShutdownMode.OnExplicitShutdown;

                if (!await EnsureGeodataAsync())
                {
                    return;
                }

                await AppHost!.StartAsync();

                var startupForm = AppHost.Services.GetRequiredService<MainWindow>();
                startupForm.Show();
                ShutdownMode = ShutdownMode.OnLastWindowClose;

                bot = AppHost.Services.GetRequiredService<Bot>();
                _ = RunBotAsync(bot);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("Startup error: " + ex);
                Shutdown(-1);
            }

            base.OnStartup(e);
        }

        /// <summary>
        /// Checks that the geodata files are present in Assets/geodata and
        /// downloads the missing ones from the public geodata repository,
        /// showing a progress window. Returns false when the app should exit.
        /// </summary>
        private async Task<bool> EnsureGeodataAsync()
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("config.json", optional: false)
                .Build();

            var downloader = new GeodataDownloader(
                config.GetValue<string>("GeoDataDirectory") ?? "geodata"
            );

            List<GeodataFile> missing;
            try
            {
                missing = await downloader.GetMissingFilesAsync();
            }
            catch (Exception ex)
            {
                var answer = System.Windows.MessageBox.Show(
                    "Unable to check geodata files online:\n" + ex.Message +
                    "\n\nContinue without geodata? Pathfinding will not work.",
                    "L2Bot 2.0",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning
                );
                if (answer == MessageBoxResult.No)
                {
                    Shutdown(-1);
                    return false;
                }
                return true;
            }

            if (missing.Count == 0)
            {
                return true;
            }

            var window = new GeodataDownloadWindow();
            window.Show();
            try
            {
                await downloader.DownloadAsync(
                    missing,
                    new Progress<GeodataDownloadProgress>(window.UpdateProgress),
                    window.CancellationToken
                );
                window.Close();
                return true;
            }
            catch (OperationCanceledException)
            {
                Shutdown(0);
                return false;
            }
            catch (Exception ex)
            {
                window.Close();
                System.Windows.MessageBox.Show(
                    "Geodata download failed:\n" + ex.Message,
                    "L2Bot 2.0",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
                Shutdown(-1);
                return false;
            }
        }

        private async Task RunBotAsync(Bot instance)
        {
            try { await instance.StartAsync(); }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show("Unable to start the game connection:\n" + ex.Message,
                    "L2Bot", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            AppHost?.Services.GetService<BotEngineInterface>()?.Stop();

            if (bot != null)
            {
                await bot.StopAsync();
            }

            if (AppHost != null)
            {
                await AppHost.StopAsync();
                AppHost.Dispose();
            }

            base.OnExit(e);
        }

        private void ConfigureServices(IServiceCollection services)
        {
            IConfiguration config = new ConfigurationBuilder()
                .AddJsonFile("config.json", optional: false, reloadOnChange: true)
                .AddJsonFile("Assets/data/experience.json", optional: false, reloadOnChange: true)
                .AddJsonFile("Assets/data/npcInfo.json", optional: false, reloadOnChange: true)
                .Build();

            services
                .AddSingleton(typeof(IConfiguration), config)
                .AddSingleton<MainWindow>()
                .AddSingleton(
                    typeof(Bot),
                    x => new Bot(
                            x.GetRequiredService<IServiceProvider>(),
                            config.GetValue<string>("DLLName") ?? ""
                        )
                    )
                .AddSingleton(typeof(EntityHandlerFactoryInterface), typeof(EntityHandlerFactory))
                .AddSingleton(typeof(MessageParserInterface), typeof(JsonMessageParser))
                .AddSingleton(typeof(OutgoingMessageBuilderInterface), typeof(JsonOutgoingMessageBuilder))
                .AddSingleton(
                    typeof(TransportInterface),
                    x => new NamedPipeTransport(
                        config.GetValue<string>("ConnectionPipeName") ?? ""
                    )
                )
                .AddSingleton(typeof(ExperienceHelperInterface), typeof(ConfigurationExperienceHelper))
                .AddSingleton(typeof(NpcInfoHelperInterface), typeof(ConfigurationNpcInfoHelper))
                .AddSingleton(typeof(EventBusInterface), typeof(InMemoryEventBus))

                .AddTransient(typeof(EntityFactoryInterface<Entity>), typeof(EntityFactory<Entity>))
                .AddTransient(typeof(EntityFactoryInterface<Hero>), typeof(EntityFactory<Hero>))
                .AddTransient(typeof(EntityFactoryInterface<AbnormalEffect>), typeof(EntityFactory<AbnormalEffect>))
                .AddTransient(typeof(EntityFactoryInterface<Drop>), typeof(EntityFactory<Drop>))
                .AddTransient(typeof(EntityFactoryInterface<NPC>), typeof(EntityFactory<NPC>))
                .AddTransient(typeof(EntityFactoryInterface<Player>), typeof(EntityFactory<Player>))
                .AddTransient(typeof(EntityFactoryInterface<ChatMessage>), typeof(EntityFactory<ChatMessage>))
                .AddTransient(typeof(EntityFactoryInterface<Skill>), typeof(EntityFactory<Skill>))
                .AddTransient(typeof(EntityFactoryInterface<ItemInterface>), typeof(ItemFactory))

                .AddSingleton<HeroHandler>()
                .AddSingleton<AbnormalEffectHandler>()
                .AddSingleton<DropHandler>()
                .AddSingleton<NpcHandler>()
                .AddSingleton<PlayerHandler>()
                .AddSingleton<ChatMessageHandler>()
                .AddSingleton<SkillHandler>()
                .AddSingleton<ItemHander>()
                .AddSingleton<WorldHandler>()

                .AddSingleton(
                    typeof(PathfinderInterface),
                    x => new L2jGeoDataPathfinder(
                        config.GetValue<string>("GeoDataDirectory") ?? "",
                        config.GetValue<ushort>("MaxPassableHeight")
                    )
                 )
                .AddSingleton(
                    typeof(AsyncPathMoverInterface),
                    x => new AsyncPathMover(
                        x.GetRequiredService<WorldHandler>(),
                        x.GetRequiredService<PathfinderInterface>(),
                        config.GetValue<int>("PathNumberOfAttempts"),
                        config.GetValue<double>("NodeWaitingTime"),
                        config.GetValue<int>("NodeDistanceTolerance"),
                        config.GetValue<int>("NextNodeDistanceTolerance")
                    )
                )

                .AddSingleton<MainViewModel>();

            services
                .AddSingleton<BotEngineInterface>(x => new BotEngine(
                    x.GetRequiredService<WorldHandler>(),
                    x.GetRequiredService<AsyncPathMoverInterface>(),
                    config.GetValue<double?>("TargetSearchRadius") ?? 1500,
                    config.GetValue<double?>("TargetApproachDistance") ?? 80));
        }
    }
}
