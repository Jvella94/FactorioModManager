// ViewModels/LogWindowViewModel.cs
using FactorioModManager.Models;
using FactorioModManager.Services.Infrastructure;
using ReactiveUI;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reactive.Disposables;

namespace FactorioModManager.ViewModels.Dialogs
{
    public class LogWindowViewModel : ViewModelBase
    {
        private readonly ILogService _logService;
        private readonly CompositeDisposable _disposables = [];

        private ObservableCollection<FormattedLogEntry> _logs;

        public ObservableCollection<FormattedLogEntry> Logs
        {
            get => _logs;
            private set => this.RaiseAndSetIfChanged(ref _logs, value);
        }

        public string LogCountText => $"{Logs.Count} log entries";

        public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
        public ReactiveCommand<Unit, Unit> ClearLogsCommand { get; }
        public ReactiveCommand<Unit, Unit> ArchiveLogsCommand { get; }
        public ReactiveCommand<Unit, Unit> OpenLogFileCommand { get; }

        public LogWindowViewModel(ILogService logService)
        {
            _logService = logService;
            _logs = [];

            // ✅ Commands instead of event handlers
            RefreshCommand = ReactiveCommand.Create(LoadLogs);
            ClearLogsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                _logService.ClearLogs();
                LoadLogs();
            });
            ArchiveLogsCommand = ReactiveCommand.CreateFromTask(async () =>
            {
                _logService.ArchiveLogs();
                LoadLogs();
            });
            OpenLogFileCommand = ReactiveCommand.Create(() =>
            {
                var logFilePath = _logService.GetLogFilePath();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = logFilePath,
                    UseShellExecute = true
                });
            });

            // Load initial logs
            LoadLogs();
        }

        private void LoadLogs()
        {
            var rawLogs = _logService.GetLogs().ToList();

            Logs.Clear();
            foreach (var log in rawLogs)
            {
                // ✅ Convert raw log to formatted entry
                Logs.Add(new FormattedLogEntry(log));
            }

            this.RaisePropertyChanged(nameof(LogCountText));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _disposables?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}