using FaceCensorApp.Application.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IMediaScanner
{
    Task<MediaScanResult> ScanAsync(string rootFolder, bool includeSubfolders, CancellationToken cancellationToken);
}