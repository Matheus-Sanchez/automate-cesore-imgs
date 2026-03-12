using System.Drawing;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Application.Contracts;

public interface IImageCensorService
{
    Task<Bitmap> ApplyAsync(Bitmap source, IReadOnlyList<DetectionBox> boxes, CensorPreset preset, CancellationToken cancellationToken);
}