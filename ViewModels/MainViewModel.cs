using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Threading;
using Microsoft.Win32;
using OmniFlow.Models;
using OmniFlow.Services;
using OmniFlow.Views;

namespace OmniFlow.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly LogChannel _channel;
    private readonly ILogWatcherFactory _watcherFactory;
    private readonly ThemeService _themeService;
    private CancellationTokenSource? _watcherCts;
    private readonly List<ILogWatcher> _activeWatchers = new();

    // Кэшируем скроллер, чтобы не искать его 50 тысяч раз
    private System.Windows.Controls.ScrollViewer? _cachedScrollViewer;

    public ObservableCollection<LogEntry> Logs { get; } = new();
    public ObservableCollection<string> ActiveFiles { get; } = new();
    public ICollectionView FilteredLogs { get; }

    private string _statusText = "Ready";
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }

    private int _totalLogs;
    public int TotalLogs { get => _totalLogs; set => SetProperty(ref _totalLogs, value); }

    private int _errorCount;
    public int ErrorCount { get => _errorCount; set => SetProperty(ref _errorCount, value); }

    private int _warnCount;
    public int WarnCount { get => _warnCount; set => SetProperty(ref _warnCount, value); }

    private string _activeFilter = "ALL";
    public string ActiveFilter
    {
        get => _activeFilter;
        set { SetProperty(ref _activeFilter, value); FilteredLogs.Refresh(); }
    }

    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { SetProperty(ref _searchText, value); UpdateSearchRegex(); FilteredLogs.Refresh(); }
    }

    private bool _isRegexValid = true;
    public bool IsRegexValid { get => _isRegexValid; set => SetProperty(ref _isRegexValid, value); }

    private bool _isPaused;
    public bool IsPaused { get => _isPaused; set => SetProperty(ref _isPaused, value); }

    private Regex? _searchRegex;
    
    public RelayCommand AddFileCommand { get; }
    public RelayCommand StartCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand RemoveFileCommand { get; }
    public RelayCommand FilterCommand { get; }
    public RelayCommand ExportCommand { get; }
    public RelayCommand ChangeThemeCommand { get; }

    public MainViewModel(LogChannel channel, ILogWatcherFactory watcherFactory, ThemeService themeService)
    {
        _channel = channel;
        _watcherFactory = watcherFactory;
        _themeService = themeService;

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
        ExportCommand = new RelayCommand(async _ => await ExportLogsAsync());
        ChangeThemeCommand = new RelayCommand(param => 
        {
            if (param is string theme)
            {
                if (theme == "Cyberpunk") _themeService.ApplyCyberpunkTheme();
                if (theme == "Ocean") _themeService.ApplyOceanTheme();
                if (theme == "Matrix") _themeService.ApplyMatrixTheme();
            }
        });

        _ = Task.Run(ReadChannelAsync);
    }

    private void UpdateSearchRegex()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) { _searchRegex = null; IsRegexValid = true; return; }
        try { _searchRegex = new Regex(SearchText, RegexOptions.Compiled | RegexOptions.IgnoreCase); IsRegexValid = true; }
        catch { _searchRegex = null; IsRegexValid = false; }
    }

    private bool FilterLogic(object obj)
    {
        if (obj is not LogEntry entry) return false;
        if (ActiveFilter == "ERROR" && !(entry.Level == "ERROR" || entry.Level == "Error")) return false;
        if (ActiveFilter == "WARN" && !(entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn")) return false;
        if (_searchRegex != null && !_searchRegex.IsMatch(entry.Message) && !_searchRegex.IsMatch(entry.Properties ?? "")) return false;
        return true;
    }

    private void AddFile()
    {
        var dlg = new OpenFileDialog { Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*", Title = "Select Log Files", Multiselect = true };
        if (dlg.ShowDialog() == true)
        {
            foreach (var file in dlg.FileNames) if (!ActiveFiles.Contains(file)) ActiveFiles.Add(file);
            StartCommand.RaiseCanExecuteChanged();
        }
    }

    private async void StartWatching()
    {
        _channel.Clear(); Logs.Clear(); TotalLogs = 0; ErrorCount = 0; WarnCount = 0;
        _cachedScrollViewer = null; // Сбрасываем кэш при новом старте
        _watcherCts = new CancellationTokenSource();
        StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged();
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
        catch (OperationCanceledException) { StatusText = "Stopped"; }
    }

    private void StopWatching()
    {
        _watcherCts?.Cancel(); _watcherCts?.Dispose(); _watcherCts = null;
        foreach (var watcher in _activeWatchers) watcher.StopWatching();
        _activeWatchers.Clear();
        StartCommand.RaiseCanExecuteChanged(); StopCommand.RaiseCanExecuteChanged();
        StatusText = "Stopped";
    }

    private async Task ExportLogsAsync()
    {
        var dlg = new SaveFileDialog { Filter = "Log files (*.log)|*.log|Text files (*.txt)|*.txt|All files (*.*)|*.*", FileName = "OmniFlow_Export.log", Title = "Export Filtered Logs" };
        if (dlg.ShowDialog() == true)
        {
            StatusText = "Exporting...";
            try
            {
                var filteredList = new List<LogEntry>();
                foreach (var item in FilteredLogs) if (item is LogEntry entry) filteredList.Add(entry);
                using (var writer = new StreamWriter(dlg.FileName))
                {
                    foreach (var entry in filteredList)
                    {
                        var line = $"[{entry.Timestamp:yyyy-MM-dd HH:mm:ss}] [{entry.Level}] [{entry.SourceFile}] {entry.Message}";
                        if (!string.IsNullOrWhiteSpace(entry.Properties)) line += $" | {entry.Properties}";
                        await writer.WriteLineAsync(line);
                    }
                }
                StatusText = "Export Complete!";
            }
            catch { StatusText = "Export Failed."; }
        }
    }

    private async Task ReadChannelAsync()
    {
        await foreach (var entry in _channel.Reader.ReadAllAsync())
        {
            while (IsPaused && !(_watcherCts?.IsCancellationRequested ?? false))
            {
                await Task.Delay(100);
            }

            if (_watcherCts?.IsCancellationRequested ?? false) break;

            // Используем BeginInvoke (асинхронно), чтобы не блокировать поток чтения файла
            Application.Current?.Dispatcher.BeginInvoke(new Action(() =>
            {
                // Жёстко ограничиваем 50к, чтобы UI не сдох
                if (Logs.Count >= 50000) return;

                // O(1) Добавление в конец. Сортировка по времени нам не нужна, 
                // так как файлы и так читаются последовательно.
                Logs.Add(entry);
                
                TotalLogs++;

                if (entry.Level == "ERROR" || entry.Level == "Error") ErrorCount++;
                if (entry.Level == "WARN" || entry.Level == "Warning" || entry.Level == "Warn") WarnCount++;

                if (!IsPaused)
                {
                    // Ищем скроллер только 1 раз!
                    if (_cachedScrollViewer == null && Application.Current.MainWindow is MainWindow mw)
                    {
                        _cachedScrollViewer = GetScrollViewer(mw.LogList);
                    }

                    if (_cachedScrollViewer != null && _cachedScrollViewer.VerticalOffset >= _cachedScrollViewer.ScrollableHeight - 50)
                    {
                        if (Application.Current.MainWindow is MainWindow mainWin)
                        {
                            mainWin.LogList.ScrollIntoView(Logs[Logs.Count - 1]);
                        }
                    }
                }
            }), DispatcherPriority.Background); // Background приоритет позволяет UI дышать
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