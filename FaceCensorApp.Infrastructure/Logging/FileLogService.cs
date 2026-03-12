using FaceCensorApp.Application.Contracts;

namespace FaceCensorApp.Infrastructure.Logging;

public sealed class FileLogService : ILogService, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string? _logFilePath;

    public async Task InitializeAsync(string logFilePath, CancellationToken cancellationToken)
    {
        _logFilePath = logFilePath;
        Directory.CreateDirectory(Path.GetDirectoryName(logFilePath)!);
        await WriteLineAsync("INFO", "Log inicializado.", cancellationToken, null);
    }

    public Task WriteInfoAsync(string message, CancellationToken cancellationToken) =>
        WriteLineAsync("INFO", message, cancellationToken, null);

    public Task WriteWarningAsync(string message, CancellationToken cancellationToken) =>
        WriteLineAsync("WARN", message, cancellationToken, null);

    public Task WriteErrorAsync(string message, Exception? exception, CancellationToken cancellationToken) =>
        WriteLineAsync("ERROR", message, cancellationToken, exception);

    private async Task WriteLineAsync(string level, string message, CancellationToken cancellationToken, Exception? exception)
    {
        if (string.IsNullOrWhiteSpace(_logFilePath))
        {
            return;
        }

        var line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{level}] {message}";
        if (exception is not null)
        {
            line = $"{line}{Environment.NewLine}{exception}";
        }

        await _gate.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_logFilePath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}