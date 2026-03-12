namespace FaceCensorApp.Application.Models;

public sealed record JobOutputContext(
    string RunId,
    string RunRoot,
    string CensoredRoot,
    string? OriginalsRoot,
    string LogsRoot,
    string LogPath,
    string SummaryPath);