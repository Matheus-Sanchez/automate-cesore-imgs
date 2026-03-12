using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record ProcessingJob
{
    public string RootFolder { get; init; } = string.Empty;

    public bool IncludeSubfolders { get; init; } = true;

    public bool KeepOriginals { get; init; }

    public OutputMode OutputMode { get; init; } = OutputMode.SeparateOutput;

    public bool CreateBackupWhenOverwriting { get; init; } = true;

    public string OutputFolderName { get; init; } = "Saida";

    public CensorPreset Preset { get; init; } = CensorPreset.Default;

    public InferenceEngineType SelectedEngine { get; init; } = InferenceEngineType.YuNetOnnx;

    public float ConfidenceThreshold { get; init; } = 0.60f;

    public float NmsThreshold { get; init; } = 0.30f;

    public int TopK { get; init; } = 5000;

    public string? ModelPath { get; init; }
}