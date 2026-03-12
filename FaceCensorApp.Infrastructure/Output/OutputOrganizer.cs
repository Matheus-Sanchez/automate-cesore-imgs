using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Infrastructure.Output;

public sealed class OutputOrganizer : IOutputOrganizer
{
    public Task<JobOutputContext> InitializeAsync(ProcessingJob job, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var runId = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var runRoot = Path.Combine(job.RootFolder, job.OutputFolderName, runId);
        var censoredRoot = Path.Combine(runRoot, "Censurados");
        var originalsRoot = (job.KeepOriginals || job.CreateBackupWhenOverwriting)
            ? Path.Combine(runRoot, "Originais")
            : null;
        var logsRoot = Path.Combine(runRoot, "logs");
        Directory.CreateDirectory(runRoot);
        Directory.CreateDirectory(censoredRoot);
        Directory.CreateDirectory(logsRoot);
        if (originalsRoot is not null)
        {
            Directory.CreateDirectory(originalsRoot);
        }

        return Task.FromResult(new JobOutputContext(
            runId,
            runRoot,
            censoredRoot,
            originalsRoot,
            logsRoot,
            Path.Combine(logsRoot, "execution-log.txt"),
            Path.Combine(logsRoot, "summary.json")));
    }

    public Task<OutputTarget> ResolveOutputAsync(JobOutputContext context, ProcessingJob job, MediaItem item, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        string censoredPath;
        string? originalBackupPath = null;

        if (job.OutputMode == OutputMode.OverwriteInPlace)
        {
            censoredPath = item.FullPath;
            if (context.OriginalsRoot is not null)
            {
                originalBackupPath = Path.Combine(context.OriginalsRoot, item.RelativePath);
            }
        }
        else
        {
            censoredPath = Path.Combine(context.CensoredRoot, item.RelativePath);
            if (job.KeepOriginals && context.OriginalsRoot is not null)
            {
                originalBackupPath = Path.Combine(context.OriginalsRoot, item.RelativePath);
            }
        }

        Directory.CreateDirectory(Path.GetDirectoryName(censoredPath)!);
        if (originalBackupPath is not null)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(originalBackupPath)!);
        }

        return Task.FromResult(new OutputTarget(censoredPath, originalBackupPath));
    }

    public Task BackupOriginalAsync(MediaItem item, OutputTarget target, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(target.OriginalBackupPath))
        {
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(target.OriginalBackupPath)!);
        if (!File.Exists(target.OriginalBackupPath))
        {
            File.Copy(item.FullPath, target.OriginalBackupPath, overwrite: false);
        }

        return Task.CompletedTask;
    }
}