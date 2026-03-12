using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record JobSummary(
    string RunId,
    string? RunRoot,
    string? SummaryPath,
    string? LogPath,
    TimeSpan Duration,
    IReadOnlyList<ProcessingResult> Results)
{
    public int ProcessedCount => Results.Count(r => r.Status == ProcessingStatus.Processed);

    public int IgnoredCount => Results.Count(r => r.Status == ProcessingStatus.Ignored);

    public int FailedCount => Results.Count(r => r.Status == ProcessingStatus.Failed);

    public int TotalFacesDetected => Results.Sum(r => r.FacesDetected);
}