using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Models;

public sealed record MediaScanResult(
    IReadOnlyList<MediaItem> Images,
    IReadOnlyList<MediaItem> IgnoredVideos,
    IReadOnlyList<string> UnsupportedFiles);