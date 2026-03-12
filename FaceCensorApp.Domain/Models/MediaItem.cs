using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record MediaItem(
    string FullPath,
    string RelativePath,
    MediaType MediaType,
    string Extension,
    long SizeBytes)
{
    public string FileName => Path.GetFileName(FullPath);
}