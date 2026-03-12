namespace FaceCensorApp.Domain.Models;

public sealed record AppSettings
{
    public string? LastRootFolder { get; init; }

    public string? LastModelPath { get; init; }

    public float DefaultConfidenceThreshold { get; init; } = 0.60f;

    public string OutputFolderName { get; init; } = "Saida";

    public CensorPreset DefaultPreset { get; init; } = CensorPreset.Default;
}