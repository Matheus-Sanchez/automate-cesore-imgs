using FaceCensorApp.Domain.Enums;

namespace FaceCensorApp.Domain.Models;

public sealed record CensorPreset(
    FilterType FilterType,
    int ColorArgb,
    float MarginPercent,
    int BlurLevel,
    int PixelBlockSize,
    float Opacity)
{
    public static CensorPreset Default =>
        new(FilterType.BlackCircle, unchecked((int)0xFF000000), 15f, 8, 12, 1f);
}