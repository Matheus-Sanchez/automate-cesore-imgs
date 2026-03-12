namespace FaceCensorApp.Application.Models;

public sealed record OutputTarget(
    string CensoredPath,
    string? OriginalBackupPath);