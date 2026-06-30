using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using OmniFlow.Models;
using OmniFlow.Services;

namespace OmniFlow.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly LogChannel _channel;
    private readonly ILogWatcherFactory _watcherFactory;
    private CancellationTokenSource? _watcherCts;
    private readonly List<ILogWatcher> _activeWatchers = new();

    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<string> ActiveFiles { get; } = new();
    public ICollectionView FilteredLogs { get; }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private int _totalLogs;
    public int TotalLogs
    {
        get => _totalLogs;
        set => SetProperty(ref _totalLogs, value);
    }

    private int _errorCount;
    public int ErrorCount
    {
        get => _errorCount;
        set => SetProperty(ref _errorCount, value);
    }

    private int _warnCount;
    public int WarnCount
    {
        get => _warnCount;
        set => SetProperty(ref _warnCount, value);
    }

    private string _activeFilter = "ALL";
    public string ActiveFilter
    {
        get => _activeFilter;
        set
        {
            SetProperty(ref _activeFilter, value);
            FilteredLogs.Refresh();
        }
    }

    public RelayCommand AddFileCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RemoveFileCommand { get; }
    public RelayCommand FilterCommand { get; }

    public MainViewModel(LogChannel channel, ILogWatcherFactory watcherFactory)
    {
        _channel = channel;
        _watcherFactory = watcherFactory;

        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = FilterLogic;

        AddFileCommand = new RelayCommand(_ => AddFile());
        StartCommand = new RelayCommand(_ => StartWatching(), _ => ActiveFiles.Count > 0 && _watcherCts is null);
        StopCommand = new RelayCommand(_ => StopWatching(), _ => _watcherCts is not null);
        RemoveFileCommand = new RelayCommand(param => 
        {
            if (param is string file) ActiveFiles.Remove(file);
            StartCommand.RaiseCanExecuteChanged();
        });
        FilterCommand = new RelayCommand(param => 
        {
            if (param is string filter) ActiveFilter = filter;
        });

        _ = Task.Run(ReadChannelAsync);
    }

    private bool FilterLogic(object obj)
    {
        if (ActiveFilter == "ALL") return true;
        if (obj is LogEntry entry)
        {
            if (ActiveFilter == "ERROR") return entry.Level == "ERROR" || entry.Level == "Error";
            if (ActiveFilter == "WARN") return entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn";
        }
        return false;
    }

    private void AddFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select Log Files",
            Multiselect = true
        };

        if (dlg.ShowDialog() == true)
        {
            foreach (var file in dlg.FileNames)
            {
                if (!ActiveFiles.Contains(file)) ActiveFiles.Add(file);
            }
            StartCommand.RaiseCanExecuteChanged();
        }
    }

    private async void StartWatching()
    {
        _channel.Clear(); // Очищаем застрявшие логи
        Logs.Clear();
        TotalLogs = 0; ErrorCount = 0; WarnCount = 0;

        _watcherCts = new CancellationTokenSource();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        StatusText = "Watching...";

        try
        {
            var tasks = new List<Task>();
            foreach (var file in ActiveFiles)
            {
                var watcher = _watcherFactory.CreateWatcher();
                _activeWatchers.Add(watcher);
                tasks.Add(watcher.StartWatchingAsync(file, _watcherCts.Token));
            }

            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Stopped";
        }
    }

    private void StopWatching()
    {
        _watcherCts?.Cancel();
        _watcherCts?.Dispose();
        _watcherCts = null;
        
        foreach (var watcher in _activeWatchers) watcher.StopWatching();
        _activeWatchers.Clear();

        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        StatusText = "Stopped";
    }

    private async Task ReadChannelAsync()
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync())
        {
            Application.Current?.Dispatcher.Invoke(() =>
            {
                if (Logs.Count >= 10000) Logs.RemoveAt(0);
                
                // Вставляем лог по таймстемпу (Merge Sort логика)
                int index = Logs.Count - 1;
                while (index >= 0 && Logs[index].Timestamp > entry.Timestamp)
                {
                    index--;
                }
                Logs.Insert(index + 1, entry);
                
                TotalLogs++;

                if (entry.Level == "ERROR" || entry.Level == "Error") ErrorCount++;
                if (entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn") WarnCount++;
            });
        }
    }
}