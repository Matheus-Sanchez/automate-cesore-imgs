using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record ProcessingResult(
    string InputPath,
    string? OutputPath,
    bool Success,
    int FacesDetected,
    IReadOnlyList<string> Errors,
    TimeSpan Duration,
    ProcessingStatus Status,
    MediaType MediaType,
    string? Notes = null);