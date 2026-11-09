using Client.Application.Commands;
using Client.Domain.Common;
using Client.Domain.Service;
using System;
using System.Collections.ObjectModel;
using System.Windows.Input;

namespace Client.Application.ViewModels
{
    /// <summary>
    /// View model for the Bot tab: Start/Stop commands, current state, engine log.
    /// </summary>
    public class BotViewModel : ObservableObject
    {
        private readonly BotEngineInterface engine;

        public BotViewModel(BotEngineInterface engine)
        {
            this.engine = engine;

            StartCommand = new RelayCommand(_ => engine.Start(), _ => !engine.IsRunning);
            StopCommand = new RelayCommand(_ => engine.Stop(), _ => engine.IsRunning);

            engine.StateChanged += _ =>
            {
                OnPropertyChanged(nameof(StateText));
                OnPropertyChanged(nameof(IsRunning));
            };
            engine.Logged += OnEngineLogged;
        }

        public string StateText => engine.State.ToString();
        public bool IsRunning => engine.IsRunning;

        public ObservableCollection<string> Log { get; } = new ObservableCollection<string>();

        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }

        private void OnEngineLogged(string line)
        {
            var entry = DateTime.Now.ToString("HH:mm:ss") + "  " + line;

            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                AddLogEntry(entry);
            }
            else
            {
                dispatcher.Invoke(() => AddLogEntry(entry));
            }
        }

        private void AddLogEntry(string entry)
        {
            Log.Insert(0, entry);
            while (Log.Count > MaxLogEntries)
            {
                Log.RemoveAt(Log.Count - 1);
            }
        }

        private const int MaxLogEntries = 200;
    }
}
