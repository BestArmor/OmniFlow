using System;
using System.Buffers;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using OmniFlow.Models;

namespace OmniFlow.Services;

public sealed class MemoryMappedLogWatcher : ILogWatcher
{
    private readonly LogChannel _channel;
    private CancellationTokenSource? _cts;

    public MemoryMappedLogWatcher(LogChannel channel)
    {
        _channel = channel;
    }

    public async Task StartWatchingAsync(string filePath, CancellationToken cancellationToken)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var token = _cts.Token;

        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(8192);
        int bytesInBuffer = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await fs.ReadAsync(buffer.AsMemory(bytesInBuffer), token);
                if (bytesRead == 0)
                {
                    if (bytesInBuffer > 0)
                    {
                        var entry = ParseLogEntry(buffer.AsSpan(0, bytesInBuffer), filePath);
                        await _channel.Writer.WriteAsync(entry, token);
                        bytesInBuffer = 0;
                    }
                    await Task.Delay(500, token);
                    continue;
                }

                bytesInBuffer += bytesRead;
                
                // Вынесли работу со Span в синхронный метод, чтобы не было ошибок C# 12
                var parsedEntries = ProcessBuffer(buffer, ref bytesInBuffer, filePath);

                foreach (var entry in parsedEntries)
                {
                    // Ждем, пока канал освободится. Никаких потерь логов!
                    await _channel.Writer.WriteAsync(entry, token);
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private List<LogEntry> ProcessBuffer(byte[] buffer, ref int bytesInBuffer, string sourceFile)
    {
        var entries = new List<LogEntry>();
        int processedIndex = 0;
        Span<byte> span = buffer.AsSpan(0, bytesInBuffer);
        
        while (true)
        {
            int newlineIndex = span.IndexOf((byte)'\n');
            if (newlineIndex < 0) break;

            var lineSpan = span.Slice(0, newlineIndex);
            if (lineSpan.Length > 0 && lineSpan[^1] == (byte)'\r')
            {
                lineSpan = lineSpan[..^1];
            }

            entries.Add(ParseLogEntry(lineSpan, sourceFile));

            span = span[(newlineIndex + 1)..];
            processedIndex += newlineIndex + 1;
        }

        int leftover = bytesInBuffer - processedIndex;
        if (leftover > 0 && processedIndex > 0)
        {
            buffer.AsSpan(processedIndex, leftover).CopyTo(buffer.AsSpan(0, leftover));
        }
        bytesInBuffer = leftover;

        return entries;
    }

    private static LogEntry ParseLogEntry(ReadOnlySpan<byte> lineSpan, string sourceFile)
    {
        string fileName = Path.GetFileName(sourceFile);

        if (lineSpan.Length > 0 && lineSpan[0] == (byte)'{')
        {
            return ParseJsonLog(lineSpan, fileName);
        }

        string line = Encoding.UTF8.GetString(lineSpan);
        return ParseTextLog(line, fileName);
    }

    private static LogEntry ParseJsonLog(ReadOnlySpan<byte> jsonSpan, string fileName)
    {
        var reader = new Utf8JsonReader(jsonSpan);
        DateTime timestamp = DateTime.Now;
        string level = "INFO";
        string message = "";
        var props = new StringBuilder();

        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    var propName = reader.GetString();
                    reader.Read();

                    switch (propName)
                    {
                        case "@t":
                        case "timestamp":
                            DateTime.TryParse(reader.GetString(), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind, out timestamp);
                            break;
                        case "@l":
                        case "level":
                            string rawLevel = reader.GetString() ?? "INFO";
                            level = rawLevel.ToLower() switch
                            {
                                "error" or "err" or "fatal" or "critical" => "ERROR",
                                "warning" or "warn" => "WARN",
                                _ => "INFO"
                            };
                            break;
                        case "@mt":
                        case "message":
                            message = reader.GetString() ?? "";
                            break;
                        default:
                            string val = reader.TokenType == JsonTokenType.String 
                                ? reader.GetString() ?? "" 
                                : Encoding.UTF8.GetString(reader.ValueSpan);
                            props.Append($"{propName}={val} ");
                            break;
                    }
                }
            }
        }
        catch
        {
            return new LogEntry(DateTime.Now, "INFO", Encoding.UTF8.GetString(jsonSpan), fileName, "");
        }

        return new LogEntry(timestamp, level, message, fileName, props.ToString().Trim());
    }

    private static LogEntry ParseTextLog(string line, string fileName)
    {
        if (line.Length > 20 && line[0] == '[')
        {
            int endBracket = line.IndexOf(']');
            if (endBracket > 0)
            {
                var dateStr = line.Substring(1, endBracket - 1);
                if (DateTime.TryParse(dateStr, out var date))
                {
                    var rest = line.AsSpan(endBracket + 1).TrimStart();
                    string level = "INFO";
                    string msg = rest.ToString();

                    if (rest.StartsWith("["))
                    {
                        int levelEnd = rest.IndexOf(']');
                        if (levelEnd > 0)
                        {
                            level = rest[1..levelEnd].ToString();
                            msg = rest[(levelEnd + 1)..].TrimStart().ToString();
                        }
                    }

                    return new LogEntry(date, level, msg, fileName, "");
                }
            }
        }

        return new LogEntry(DateTime.Now, "INFO", line, fileName, "");
    }

    public void StopWatching()
    {
        _cts?.Cancel();
    }
}

public sealed class MemoryMappedLogWatcherFactory : ILogWatcherFactory
{
    private readonly LogChannel _channel;

    public MemoryMappedLogWatcherFactory(LogChannel channel)
    {
        _channel = channel;
    }

    public ILogWatcher CreateWatcher() => new MemoryMappedLogWatcher(_channel);
}