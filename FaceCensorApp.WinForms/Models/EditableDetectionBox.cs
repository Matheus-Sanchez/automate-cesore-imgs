using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.WinForms.Models;

public sealed class EditableDetectionBox
{
    public EditableDetectionBox(DetectionBox box, bool isManual)
    {
        Box = box;
        IsManual = isManual;
    }

    public DetectionBox Box { get; set; }

    public bool IsManual { get; }

    public EditableDetectionBox Clone() =>
        new(new DetectionBox(Box.X, Box.Y, Box.Width, Box.Height, Box.Confidence, Box.Label), IsManual);
}