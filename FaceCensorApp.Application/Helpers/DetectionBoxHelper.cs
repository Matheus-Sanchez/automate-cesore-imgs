using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Helpers;

public static class DetectionBoxHelper
{
    public static IReadOnlyList<DetectionBox> ExpandAndClamp(
        IEnumerable<DetectionBox> boxes,
        int imageWidth,
        int imageHeight,
        float marginPercent)
    {
        return boxes
            .Select(box => ExpandAndClamp(box, imageWidth, imageHeight, marginPercent))
            .Where(box => !box.IsEmpty)
            .ToList();
    }

    public static DetectionBox ExpandAndClamp(
        DetectionBox box,
        int imageWidth,
        int imageHeight,
        float marginPercent)
    {
        var horizontalMargin = box.Width * (marginPercent / 100f);
        var verticalMargin = box.Height * (marginPercent / 100f);

        var x = Math.Max(0f, box.X - horizontalMargin);
        var y = Math.Max(0f, box.Y - verticalMargin);
        var right = Math.Min(imageWidth, box.Right + horizontalMargin);
        var bottom = Math.Min(imageHeight, box.Bottom + verticalMargin);

        return new DetectionBox(x, y, Math.Max(0f, right - x), Math.Max(0f, bottom - y), box.Confidence, box.Label);
    }
}