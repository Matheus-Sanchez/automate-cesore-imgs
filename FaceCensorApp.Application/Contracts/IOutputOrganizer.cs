using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IOutputOrganizer
{
    Task<JobOutputContext> InitializeAsync(ProcessingJob job, CancellationToken cancellationToken);

    Task<OutputTarget> ResolveOutputAsync(JobOutputContext context, ProcessingJob job, MediaItem item, CancellationToken cancellationToken);

    Task BackupOriginalAsync(MediaItem item, OutputTarget target, CancellationToken cancellationToken);
}