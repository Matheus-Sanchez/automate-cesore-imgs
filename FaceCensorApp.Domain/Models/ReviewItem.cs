using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record ReviewItem(
    MediaItem MediaItem,
    ReviewReason Reason,
    IReadOnlyList<DetectionBox> SuggestedBoxes,
    CensorPreset Preset,
    float ConfidenceThreshold,
    string Message);