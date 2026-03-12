namespace FaceCensorApp.Domain.Models;

public sealed record ReviewDecision(
    bool ProcessItem,
    IReadOnlyList<DetectionBox> FinalBoxes,
    string? Notes = null)
{
    public static ReviewDecision Ignore(string? notes = null) =>
        new(false, Array.Empty<DetectionBox>(), notes);

    public static ReviewDecision Process(IReadOnlyList<DetectionBox> finalBoxes, string? notes = null) =>
        new(true, finalBoxes, notes);
}