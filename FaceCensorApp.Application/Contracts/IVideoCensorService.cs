using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IVideoCensorService
{
    Task<ProcessingResult> ProcessAsync(MediaItem mediaItem, ProcessingJob job, CancellationToken cancellationToken);
}