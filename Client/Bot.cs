using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Client.Application.ViewModels;
using Client.Domain.Events;
using Client.Domain.Factories;
using Client.Domain.Parsers;
using Client.Domain.Service;
using Client.Domain.Transports;
using Microsoft.Extensions.DependencyInjection;
using System.Windows;

namespace Client
{
    public class Bot
    {
        [DllImport("kernel32.dll", EntryPoint = "LoadLibraryW", CharSet = CharSet.Unicode, SetLastError = true)]
        static extern IntPtr LoadLibrary(string lpLibFileName);

        private readonly TransportInterface transport;
        private readonly MessageParserInterface messageParser;
        private readonly EntityHandlerFactoryInterface entityHandlerFactory;
        private readonly EventBusInterface eventBus;
        private readonly IServiceProvider serviceProvider;
        private readonly string dllName;
        private CancellationTokenSource? cancellationTokenSource;
        private Task? workerTask;
        private bool handlersSubscribed;

        public Bot(
            IServiceProvider serviceProvider,
            string dllName
        )
        {
            transport = serviceProvider.GetRequiredService<TransportInterface>();
            messageParser = serviceProvider.GetRequiredService<MessageParserInterface>();
            entityHandlerFactory = serviceProvider.GetRequiredService<EntityHandlerFactoryInterface>();
            eventBus = serviceProvider.GetRequiredService<EventBusInterface>();
            this.serviceProvider = serviceProvider;
            this.dllName = dllName;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            if (workerTask != null && !workerTask.IsCompleted)
            {
                return workerTask;
            }

            cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            workerTask = RunAsync(cancellationTokenSource.Token);
            return workerTask;
        }

        public async Task StopAsync()
        {
            if (cancellationTokenSource == null)
            {
                return;
            }

            cancellationTokenSource.Cancel();

            if (workerTask != null)
            {
                try
                {
                    await workerTask;
                }
                catch (OperationCanceledException)
                {
                }
            }

            transport.Message -= OnMessage;

            if (transport is IDisposable disposableTransport)
            {
                disposableTransport.Dispose();
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
            workerTask = null;
        }

        private void SubscribeAllHandlers()
        {
            var viewModel = serviceProvider.GetRequiredService<MainViewModel>();
            eventBus.Subscrbe((EventHandlerInterface<HeroCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<HeroDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<CreatureCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<CreatureDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<DropCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<DropDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ChatMessageCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<SkillCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<SkillDeletedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ItemCreatedEvent>)viewModel);
            eventBus.Subscrbe((EventHandlerInterface<ItemDeletedEvent>)viewModel);

            var worldHandler = serviceProvider.GetRequiredService<WorldHandler>();
            eventBus.Subscrbe((EventHandlerInterface<HeroCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<HeroDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<CreatureCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<CreatureDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<DropCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<DropDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<SkillCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<SkillDeletedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<ItemCreatedEvent>)worldHandler);
            eventBus.Subscrbe((EventHandlerInterface<ItemDeletedEvent>)worldHandler);

            eventBus.Subscrbe((EventHandlerInterface<TargetChangedEvent>)serviceProvider.GetRequiredService<HeroHandler>());
            eventBus.Subscrbe((EventHandlerInterface<TargetChangedEvent>)serviceProvider.GetRequiredService<NpcHandler>());
            eventBus.Subscrbe((EventHandlerInterface<TargetChangedEvent>)serviceProvider.GetRequiredService<PlayerHandler>());
        }

        private void OnMessage(string args)
        {
            if (System.Windows.Application.Current?.Dispatcher != null)
            {
                _ = System.Windows.Application.Current.Dispatcher.InvokeAsync(() => ProcessMessage(args));
                return;
            }

            ProcessMessage(args);
        }

        private void ProcessMessage(string args)
        {
            try
            {
                var message = messageParser.Parse(args);
                try
                {
                    var handler = entityHandlerFactory.GetHandler(message.Type);
                    handler.Update(message.Operation, message.Content);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Exception: " + ex.Message);
                }
            }
            catch (Domain.Exception.ParserException)
            {
                Debug.WriteLine("Unable to parse message: " + args);
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken)
        {
            IntPtr hDll = LoadLibrary(dllName);
            if (hDll == IntPtr.Zero)
            {
                throw new Exception("Unable to load library " + dllName + ": " + Marshal.GetLastWin32Error().ToString());
            }

            Debug.WriteLine(dllName + " loaded\n");
            transport.Message += OnMessage;

            if (!handlersSubscribed)
            {
                SubscribeAllHandlers();
                handlersSubscribed = true;
            }

            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await transport.ConnectAsync(cancellationToken);
                    if (!transport.IsConnected())
                    {
                        await Task.Delay(500, cancellationToken);
                        continue;
                    }

                    await transport.SendAsync("invalidate", cancellationToken);
                    await transport.StartReceiveAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Transport loop exception: " + ex);
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }
}
