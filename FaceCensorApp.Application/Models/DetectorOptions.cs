namespace FaceCensorApp.Application.Models;

public sealed record DetectorOptions(
    string ModelPath,
    float ScoreThreshold,
    float ReviewThreshold,
    float NmsThreshold,
    int TopK);