using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Infrastructure.Video;

public sealed class UnsupportedVideoCensorService : IVideoCensorService
{
    public Task<ProcessingResult> ProcessAsync(MediaItem mediaItem, ProcessingJob job, CancellationToken cancellationToken)
    {
        var result = new ProcessingResult(
            mediaItem.FullPath,
            null,
            false,
            0,
            new[] { "Video nao suportado na V1." },
            TimeSpan.Zero,
            ProcessingStatus.Ignored,
            mediaItem.MediaType,
            "Video nao suportado na V1.");
        return Task.FromResult(result);
    }
}