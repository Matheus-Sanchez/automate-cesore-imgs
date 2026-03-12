using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;

namespace FaceCensorApp.Infrastructure.Imaging;

public sealed class GdiImageCensorService : IImageCensorService
{
    public Task<Bitmap> ApplyAsync(Bitmap source, IReadOnlyList<DetectionBox> boxes, CensorPreset preset, CancellationToken cancellationToken)
    {
        var target = new Bitmap(source.Width, source.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(target))
        {
            graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
        }

        foreach (var box in boxes)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var rect = Rectangle.Round(new RectangleF(box.X, box.Y, box.Width, box.Height));
            rect = Rectangle.Intersect(rect, new Rectangle(0, 0, target.Width, target.Height));
            if (rect.Width <= 0 || rect.Height <= 0)
            {
                continue;
            }

            switch (preset.FilterType)
            {
                case FilterType.BlackCircle:
                    ApplyEllipse(target, rect, Color.FromArgb(preset.ColorArgb), preset.Opacity);
                    break;
                case FilterType.SolidRectangle:
                    ApplyRectangle(target, rect, Color.FromArgb(preset.ColorArgb), preset.Opacity);
                    break;
                case FilterType.Pixelate:
                    ApplyPixelate(target, rect, preset.PixelBlockSize, preset.Opacity);
                    break;
                case FilterType.Blur:
                    ApplyBlur(target, rect, preset.BlurLevel, preset.Opacity);
                    break;
            }
        }

        return Task.FromResult(target);
    }

    private static void ApplyEllipse(Bitmap target, Rectangle rect, Color color, float opacity)
    {
        using var graphics = Graphics.FromImage(target);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using var brush = new SolidBrush(Color.FromArgb(ToAlpha(opacity), color));
        graphics.FillEllipse(brush, rect);
    }

    private static void ApplyRectangle(Bitmap target, Rectangle rect, Color color, float opacity)
    {
        using var graphics = Graphics.FromImage(target);
        using var brush = new SolidBrush(Color.FromArgb(ToAlpha(opacity), color));
        graphics.FillRectangle(brush, rect);
    }

    private static void ApplyPixelate(Bitmap target, Rectangle rect, int blockSize, float opacity)
    {
        using var original = CopyRegion(target, rect);
        using var reduced = new Bitmap(Math.Max(1, rect.Width / Math.Max(1, blockSize)), Math.Max(1, rect.Height / Math.Max(1, blockSize)));
        using (var graphics = Graphics.FromImage(reduced))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(original, new Rectangle(0, 0, reduced.Width, reduced.Height));
        }

        using var effect = new Bitmap(rect.Width, rect.Height);
        using (var graphics = Graphics.FromImage(effect))
        {
            graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
            graphics.PixelOffsetMode = PixelOffsetMode.Half;
            graphics.DrawImage(reduced, new Rectangle(0, 0, effect.Width, effect.Height));
        }

        BlendRegion(target, rect, effect, opacity);
    }

    private static void ApplyBlur(Bitmap target, Rectangle rect, int blurLevel, float opacity)
    {
        using var original = CopyRegion(target, rect);
        var scale = Math.Clamp(blurLevel, 2, 24);
        using var reduced = new Bitmap(Math.Max(1, rect.Width / scale), Math.Max(1, rect.Height / scale));
        using (var graphics = Graphics.FromImage(reduced))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(original, new Rectangle(0, 0, reduced.Width, reduced.Height));
        }

        using var effect = new Bitmap(rect.Width, rect.Height);
        using (var graphics = Graphics.FromImage(effect))
        {
            graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
            graphics.DrawImage(reduced, new Rectangle(0, 0, effect.Width, effect.Height));
        }

        BlendRegion(target, rect, effect, opacity);
    }

    private static Bitmap CopyRegion(Bitmap source, Rectangle rect)
    {
        var result = new Bitmap(rect.Width, rect.Height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(result);
        graphics.DrawImage(source, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
        return result;
    }

    private static void BlendRegion(Bitmap target, Rectangle rect, Image effect, float opacity)
    {
        using var graphics = Graphics.FromImage(target);
        if (opacity >= 0.99f)
        {
            graphics.DrawImage(effect, rect);
            return;
        }

        using var attributes = new ImageAttributes();
        var matrix = new ColorMatrix { Matrix33 = Math.Clamp(opacity, 0f, 1f) };
        attributes.SetColorMatrix(matrix, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
        graphics.DrawImage(effect, rect, 0, 0, effect.Width, effect.Height, GraphicsUnit.Pixel, attributes);
    }

    private static int ToAlpha(float opacity) => (int)Math.Round(Math.Clamp(opacity, 0f, 1f) * 255f);
}