using System.Threading.Channels;
using OmniFlow.Models;

namespace OmniFlow.Services;

public sealed class LogChannel
{
    // Ограничиваем очередь до 5000 элементов. 
    // Если UI завис, ридер не сожрет всю память, а будет ждать.
    private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true, // UI поток один
            SingleWriter = false // Файлов может быть много
        });

    public ChannelReader<LogEntry> Reader => _channel.Reader;
    public ChannelWriter<LogEntry> Writer => _channel.Writer;
}