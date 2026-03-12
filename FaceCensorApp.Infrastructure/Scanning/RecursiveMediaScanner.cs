using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Infrastructure.Scanning;

public sealed class RecursiveMediaScanner : IMediaScanner
{
    private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".bmp"
    };

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".avi", ".mov", ".mkv"
    };

    private static readonly HashSet<string> IgnoredDirectories = new(StringComparer.OrdinalIgnoreCase)
    {
        "Saida", "Originais", "Censurados", "logs"
    };

    public Task<MediaScanResult> ScanAsync(string rootFolder, bool includeSubfolders, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(rootFolder))
        {
            throw new DirectoryNotFoundException($"Pasta raiz nao encontrada: {rootFolder}");
        }

        var images = new List<MediaItem>();
        var videos = new List<MediaItem>();
        var unsupported = new List<string>();

        ScanDirectory(rootFolder, rootFolder, includeSubfolders, images, videos, unsupported, cancellationToken);
        return Task.FromResult(new MediaScanResult(
            images.OrderBy(item => item.RelativePath).ToList(),
            videos.OrderBy(item => item.RelativePath).ToList(),
            unsupported.OrderBy(path => path).ToList()));
    }

    private static void ScanDirectory(
        string currentDirectory,
        string rootFolder,
        bool includeSubfolders,
        ICollection<MediaItem> images,
        ICollection<MediaItem> videos,
        ICollection<string> unsupported,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        foreach (var filePath in Directory.EnumerateFiles(currentDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var extension = Path.GetExtension(filePath);
            var relativePath = Path.GetRelativePath(rootFolder, filePath);
            var fileInfo = new FileInfo(filePath);

            if (ImageExtensions.Contains(extension))
            {
                images.Add(new MediaItem(filePath, relativePath, MediaType.Image, extension, fileInfo.Length));
            }
            else if (VideoExtensions.Contains(extension))
            {
                videos.Add(new MediaItem(filePath, relativePath, MediaType.Video, extension, fileInfo.Length));
            }
            else
            {
                unsupported.Add(relativePath);
            }
        }

        if (!includeSubfolders)
        {
            return;
        }

        foreach (var childDirectory in Directory.EnumerateDirectories(currentDirectory))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var directoryName = Path.GetFileName(childDirectory);
            if (IgnoredDirectories.Contains(directoryName))
            {
                continue;
            }

            ScanDirectory(childDirectory, rootFolder, includeSubfolders, images, videos, unsupported, cancellationToken);
        }
    }
}