using System;
using System.Buffers;
using System.IO;
using System.Text;
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

        // Открываем с FileShare.ReadWrite, чтобы не блокировать логгеры, которые пишут в этот файл
        using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        
        // Буфер 4 КБ. Аллоцируем один раз!
        byte[] buffer = ArrayPool<byte>.Shared.Rent(4096);
        int bytesInBuffer = 0;

        try
        {
            // Сначала дочитываем существующий файл до конца
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await fs.ReadAsync(buffer.AsMemory(bytesInBuffer), token);
                if (bytesRead == 0) break; // Достигнут конец файла

                bytesInBuffer += bytesRead;
                ProcessBuffer(buffer, ref bytesInBuffer, filePath, token);
            }

            // Режим tail -f: ждём появления новых данных
            while (!token.IsCancellationRequested)
            {
                int bytesRead = await fs.ReadAsync(buffer.AsMemory(bytesInBuffer), token);
                if (bytesRead == 0)
                {
                    await Task.Delay(500, token); // Спим, чтобы не жрать CPU
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
        // Ищем переносы строк в буфере
        Span<byte> span = buffer.AsSpan(0, bytesInBuffer);
        
        while (!token.IsCancellationRequested)
        {
            int newlineIndex = span.IndexOf((byte)'\n');
            if (newlineIndex < 0) break; // Полных строк больше нет

            // Вырезаем строку без \n и \r
            var lineSpan = span.Slice(0, newlineIndex);
            if (lineSpan.Length > 0 && lineSpan[^1] == (byte)'\r')
            {
                lineSpan = lineSpan[..^1];
            }

            // Декодируем в строку (здесь происходит аллокация, но только для готовой строки)
            string line = Encoding.UTF8.GetString(lineSpan);
            
            // Парсим и пушим в канал
            var entry = ParseLogEntry(line, sourceFile);
            _channel.Writer.TryWrite(entry);

            // Сдвигаем буфер: удаляем обработанную строку, оставляем остаток
            span = span[(newlineIndex + 1)..];
            bytesInBuffer = span.Length;
            
            // Копируем остаток в начало буфера
            span.CopyTo(buffer.AsSpan());
        }
    }

    // Простейший парсер. В реальности тут могут быть регулярки, но мы делаем быстро
    private static LogEntry ParseLogEntry(string line, string sourceFile)
    {
        // Предполагаем формат: [2023-10-25 12:00:00] [INFO] Message
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

                    return new LogEntry(date, level, msg, Path.GetFileName(sourceFile));
                }
            }
        }

        return new LogEntry(DateTime.Now, "INFO", line, Path.GetFileName(sourceFile));
    }

    public void StopWatching()
    {
        _cts?.Cancel();
    }
}