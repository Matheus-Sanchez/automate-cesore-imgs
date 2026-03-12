namespace FaceCensorApp.Domain.Models;

public sealed record DetectionBox(
    float X,
    float Y,
    float Width,
    float Height,
    float Confidence,
    string Label = "face")
{
    public float Right => X + Width;

    public float Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;
}