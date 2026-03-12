using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IJobExecutor
{
    Task<JobSummary> ExecuteAsync(
        ProcessingJob job,
        IProgress<JobProgress>? progress,
        Func<ReviewItem, CancellationToken, Task<ReviewDecision>>? reviewHandler,
        CancellationToken cancellationToken);
}