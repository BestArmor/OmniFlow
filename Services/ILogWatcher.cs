using System.Threading;
using System.Threading.Tasks;

namespace OmniFlow.Services;

public interface ILogWatcher
{
    Task StartWatchingAsync(string filePath, CancellationToken cancellationToken);
    void StopWatching();
}

public interface ILogWatcherFactory
{
    ILogWatcher CreateWatcher();
}