using System.Drawing;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IFaceDetector
{
    Task<IReadOnlyList<DetectionBox>> DetectAsync(Bitmap image, DetectorOptions options, CancellationToken cancellationToken);
}