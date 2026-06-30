using System.Threading.Channels;
using OmniFlow.Models;

namespace OmniFlow.Services;

public sealed class LogChannel
{
    private readonly Channel<LogEntry> _channel = Channel.CreateBounded<LogEntry>(
        new BoundedChannelOptions(5000)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        });

    public ChannelReader<LogEntry> Reader => _channel.Reader;
    public ChannelWriter<LogEntry> Writer => _channel.Writer;

    public void Clear()
    {
        while (_channel.Reader.TryRead(out _)) { }
    }
}