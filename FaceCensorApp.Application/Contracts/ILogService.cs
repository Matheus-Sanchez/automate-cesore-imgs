namespace FaceCensorApp.Application.Contracts;

public interface ILogService
{
    Task InitializeAsync(string logFilePath, CancellationToken cancellationToken);

    Task WriteInfoAsync(string message, CancellationToken cancellationToken);

    Task WriteWarningAsync(string message, CancellationToken cancellationToken);

    Task WriteErrorAsync(string message, Exception? exception, CancellationToken cancellationToken);
}