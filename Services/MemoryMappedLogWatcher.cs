using System;
using System.Buffers;
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
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        int bytesInBuffer = 0;

        try
        {
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await fs.ReadAsync(buffer.AsMemory(bytesInBuffer), token);
                if (bytesRead == 0)
                {
                    await Task.Delay(500, token);
                    continue;
                }

                bytesInBuffer += bytesRead;
                ProcessBuffer(buffer, ref bytesInBuffer, filePath, token);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private void ProcessBuffer(byte[] buffer, ref int bytesInBuffer, string sourceFile, CancellationToken token)
    {
        Span<byte> span = buffer.AsSpan(0, bytesInBuffer);
        
        while (!token.IsCancellationRequested)
        {
            int newlineIndex = span.IndexOf((byte)'\n');
            if (newlineIndex < 0) break;

            var lineSpan = span.Slice(0, newlineIndex);
            if (lineSpan.Length > 0 && lineSpan[^1] == (byte)'\r')
            {
                lineSpan = lineSpan[..^1];
            }

            var entry = ParseLogEntry(lineSpan, sourceFile);
            _channel.Writer.TryWrite(entry);

            span = span[(newlineIndex + 1)..];
            bytesInBuffer = span.Length;
            span.CopyTo(buffer.AsSpan());
        }
    }

    private static LogEntry ParseLogEntry(ReadOnlySpan<byte> lineSpan, string sourceFile)
    {
        string fileName = Path.GetFileName(sourceFile);

        // Если строка начинается с { — это JSON (Serilog/Structured log)
        if (lineSpan.Length > 0 && lineSpan[0] == (byte)'{')
        {
            return ParseJsonLog(lineSpan, fileName);
        }

        // Обычный текстовый лог
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
                            DateTime.TryParse(reader.GetString(), out timestamp);
                            break;
                        case "@l":
                        case "level":
                            string rawLevel = reader.GetString() ?? "INFO";
                            // Маппим все возможные варианты в наши стандартные
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
                            // Безопасно читаем любые значения (строки, числа, булы) без крашей
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