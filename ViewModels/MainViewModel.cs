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
    private readonly ILogWatcher _logWatcher;
    private CancellationTokenSource? _watcherCts;

    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ICollectionView FilteredLogs { get; }

    private string _statusText = "Ready";
    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private string _filePath = "";
    public string FilePath
    {
        get => _filePath;
        set
        {
            SetProperty(ref _filePath, value);
            StartCommand.RaiseCanExecuteChanged();
        }
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

    public RelayCommand BrowseCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand FilterCommand { get; }

    public MainViewModel(LogChannel channel, ILogWatcher logWatcher)
    {
        _channel = channel;
        _logWatcher = logWatcher;

        FilteredLogs = CollectionViewSource.GetDefaultView(Logs);
        FilteredLogs.Filter = FilterLogic;

        BrowseCommand = new RelayCommand(_ => BrowseFile());
        StartCommand = new RelayCommand(_ => StartWatching(), _ => string.IsNullOrEmpty(FilePath) is false && _watcherCts is null);
        StopCommand = new RelayCommand(_ => StopWatching(), _ => _watcherCts is not null);
        FilterCommand = new RelayCommand(param => 
        {
            if (param is string filter) ActiveFilter = filter;
        });

        _ = Task.Run(ReadChannelAsync);
    }

    private bool FilterLogic(object obj)
    {
        if (ActiveFilter == "ALL") return true;
        if (obj is LogEntry entry) return entry.Level == ActiveFilter;
        return false;
    }

    private void BrowseFile()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*",
            Title = "Select Log File"
        };

        if (dlg.ShowDialog() == true)
        {
            FilePath = dlg.FileName;
        }
    }

    private async void StartWatching()
    {
        Logs.Clear();
        TotalLogs = 0; ErrorCount = 0; WarnCount = 0;

        _watcherCts = new CancellationTokenSource();
        StartCommand.RaiseCanExecuteChanged();
        StopCommand.RaiseCanExecuteChanged();
        StatusText = "Watching...";

        try
        {
            await _logWatcher.StartWatchingAsync(FilePath, _watcherCts.Token);
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
                if (Logs.Count >= 10000)
                {
                    Logs.RemoveAt(0);
                }
                
                Logs.Add(entry);
                TotalLogs++;

                if (entry.Level == "ERROR") ErrorCount++;
                if (entry.Level == "WARN") WarnCount++;
            });
        }
    }
}