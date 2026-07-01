using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using Microsoft.Win32;
using OmniFlow.Models;
using OmniFlow.Services;
using OmniFlow.Views;

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

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set
        {
            SetProperty(ref _searchText, value);
            UpdateSearchRegex();
            FilteredLogs.Refresh();
        }
    }

    private bool _isRegexValid = true;
    public bool IsRegexValid
    {
        get => _isRegexValid;
        set => SetProperty(ref _isRegexValid, value);
    }

    private bool _isPaused;
    public bool IsPaused
    {
        get => _isPaused;
        set => SetProperty(ref _isPaused, value);
    }

    private Regex? _searchRegex;
    
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

    private void UpdateSearchRegex()
    {
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            _searchRegex = null;
            IsRegexValid = true;
            return;
        }

        try
        {
            _searchRegex = new Regex(SearchText, RegexOptions.Compiled | RegexOptions.IgnoreCase);
            IsRegexValid = true;
        }
        catch
        {
            _searchRegex = null;
            IsRegexValid = false;
        }
    }

    private bool FilterLogic(object obj)
    {
        if (obj is not LogEntry entry) return false;

        if (ActiveFilter == "ERROR" && !(entry.Level == "ERROR" || entry.Level == "Error")) return false;
        if (ActiveFilter == "WARN" && !(entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn")) return false;

        if (_searchRegex != null)
        {
            if (!_searchRegex.IsMatch(entry.Message) && !_searchRegex.IsMatch(entry.Properties ?? ""))
                return false;
        }

        return true;
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
        _channel.Clear();
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
                if (IsPaused) return;
                
                if (Logs.Count >= 10000) Logs.RemoveAt(0);
                
                int index = Logs.Count - 1;
                while (index >= 0 && Logs[index].Timestamp > entry.Timestamp)
                {
                    index--;
                }
                Logs.Insert(index + 1, entry);
                
                TotalLogs++;

                if (entry.Level == "ERROR" || entry.Level == "Error") ErrorCount++;
                if (entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn") WarnCount++;

                if (!IsPaused && Application.Current.MainWindow is MainWindow mw)
                {
                    var scrollViewer = GetScrollViewer(mw.LogList);
                    if (scrollViewer != null && scrollViewer.VerticalOffset >= scrollViewer.ScrollableHeight - 50)
                    {
                        mw.LogList.ScrollIntoView(Logs[Logs.Count - 1]);
                    }
                }
            });
        }
    }

    private static System.Windows.Controls.ScrollViewer? GetScrollViewer(System.Windows.DependencyObject obj)
    {
        if (obj is System.Windows.Controls.ScrollViewer) return (System.Windows.Controls.ScrollViewer)obj;
        for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
            var result = GetScrollViewer(child);
            if (result != null) return result;
        }
        return null;
    }
}