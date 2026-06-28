using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using OmniFlow.Services;
using OmniFlow.ViewModels;
using OmniFlow.Views;

namespace OmniFlow;

public partial class App : Application
{
    private readonly IServiceProvider _services;

    public App()
    {
        _services = ConfigureServices();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        
        var mainWindow = _services.GetRequiredService<MainWindow>();
        mainWindow.DataContext = _services.GetRequiredService<MainViewModel>();
        mainWindow.Show();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<LogChannel>();
        services.AddSingleton<ILogWatcher, MemoryMappedLogWatcher>();

        services.AddSingleton<MainViewModel>();

        services.AddSingleton<MainWindow>();

        return services.BuildServiceProvider();
    }
}