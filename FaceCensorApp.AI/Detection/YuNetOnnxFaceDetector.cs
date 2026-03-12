using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Models;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace FaceCensorApp.AI.Detection;

public sealed class YuNetOnnxFaceDetector : IFaceDetector, IDisposable
{
    private static readonly int[] Strides = [8, 16, 32];
    private readonly ConcurrentDictionary<string, DetectorSessionContext> _sessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<YuNetOnnxFaceDetector> _logger;

    public YuNetOnnxFaceDetector(ILogger<YuNetOnnxFaceDetector> logger)
    {
        _logger = logger;
    }

    public Task<IReadOnlyList<DetectionBox>> DetectAsync(Bitmap image, DetectorOptions options, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(options.ModelPath))
        {
            throw new FileNotFoundException("Modelo ONNX nao encontrado.", options.ModelPath);
        }

        var session = _sessions.GetOrAdd(options.ModelPath, CreateSession);
        using var prepared = PrepareInput(image, session);
        using var input = NamedOnnxValue.CreateFromTensor(session.InputName, prepared.Tensor);
        using IDisposableReadOnlyCollection<DisposableNamedOnnxValue> results = session.Session.Run([input]);

        var outputMap = results.ToDictionary(
            result => result.Name,
            result => TensorData.FromNamedValue(result),
            StringComparer.OrdinalIgnoreCase);

        var proposals = DecodeOutputs(outputMap, prepared, options, image.Width, image.Height);
        var finalDetections = ApplyNms(proposals, options.NmsThreshold, options.TopK);
        return Task.FromResult<IReadOnlyList<DetectionBox>>(finalDetections);
    }

    private DetectorSessionContext CreateSession(string modelPath)
    {
        _logger.LogInformation("Carregando modelo ONNX: {ModelPath}", modelPath);
        var sessionOptions = new SessionOptions
        {
            GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
            EnableMemoryPattern = true,
            ExecutionMode = ExecutionMode.ORT_SEQUENTIAL
        };

        var session = new InferenceSession(modelPath, sessionOptions);
        var input = session.InputMetadata.First();
        var dimensions = input.Value.Dimensions;
        var inputWidth = dimensions.Count > 3 && dimensions[3] > 0 ? dimensions[3] : null;
        var inputHeight = dimensions.Count > 2 && dimensions[2] > 0 ? dimensions[2] : null;

        return new DetectorSessionContext(session, input.Key, inputWidth, inputHeight);
    }

    private static PreparedInput PrepareInput(Bitmap image, DetectorSessionContext session)
    {
        var paddedWidth = AlignTo32(image.Width);
        var paddedHeight = AlignTo32(image.Height);

        using var paddedBitmap = new Bitmap(paddedWidth, paddedHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(paddedBitmap))
        {
            graphics.Clear(Color.Black);
            graphics.DrawImage(image, new Rectangle(0, 0, image.Width, image.Height));
        }

        var modelWidth = session.InputWidth ?? paddedWidth;
        var modelHeight = session.InputHeight ?? paddedHeight;
        var scale = session.InputWidth is null || session.InputHeight is null
            ? 1f
            : Math.Min(modelWidth / (float)paddedWidth, modelHeight / (float)paddedHeight);
        var resizedWidth = Math.Max(1, (int)Math.Round(paddedWidth * scale));
        var resizedHeight = Math.Max(1, (int)Math.Round(paddedHeight * scale));
        var offsetX = (modelWidth - resizedWidth) / 2f;
        var offsetY = (modelHeight - resizedHeight) / 2f;

        var workingBitmap = new Bitmap(modelWidth, modelHeight, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(workingBitmap))
        {
            graphics.Clear(Color.Black);
            graphics.InterpolationMode = scale == 1f ? System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor : System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
            graphics.DrawImage(paddedBitmap, new RectangleF(offsetX, offsetY, resizedWidth, resizedHeight));
        }

        var tensor = ToTensor(workingBitmap);
        return new PreparedInput(workingBitmap, tensor, modelWidth, modelHeight, scale, offsetX, offsetY);
    }

    private static DenseTensor<float> ToTensor(Bitmap bitmap)
    {
        using var converted = new Bitmap(bitmap.Width, bitmap.Height, PixelFormat.Format24bppRgb);
        using (var graphics = Graphics.FromImage(converted))
        {
            graphics.DrawImage(bitmap, new Rectangle(0, 0, converted.Width, converted.Height));
        }

        var tensor = new DenseTensor<float>([1, 3, converted.Height, converted.Width]);
        var rect = new Rectangle(0, 0, converted.Width, converted.Height);
        var bitmapData = converted.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
        try
        {
            unsafe
            {
                var scan0 = (byte*)bitmapData.Scan0;
                for (var y = 0; y < converted.Height; y++)
                {
                    var row = scan0 + (y * bitmapData.Stride);
                    for (var x = 0; x < converted.Width; x++)
                    {
                        var pixel = row + (x * 3);
                        tensor[0, 0, y, x] = pixel[0];
                        tensor[0, 1, y, x] = pixel[1];
                        tensor[0, 2, y, x] = pixel[2];
                    }
                }
            }
        }
        finally
        {
            converted.UnlockBits(bitmapData);
        }

        return tensor;
    }

    private static List<DetectionBox> DecodeOutputs(
        IReadOnlyDictionary<string, TensorData> outputMap,
        PreparedInput prepared,
        DetectorOptions options,
        int imageWidth,
        int imageHeight)
    {
        var proposals = new List<DetectionBox>();

        foreach (var stride in Strides)
        {
            if (!outputMap.TryGetValue($"cls_{stride}", out var cls) ||
                !outputMap.TryGetValue($"obj_{stride}", out var obj) ||
                !outputMap.TryGetValue($"bbox_{stride}", out var bbox))
            {
                continue;
            }

            var rows = prepared.ModelHeight / stride;
            var cols = prepared.ModelWidth / stride;

            for (var row = 0; row < rows; row++)
            {
                for (var col = 0; col < cols; col++)
                {
                    var anchorIndex = row * cols + col;
                    var clsScore = ReadValue(cls, anchorIndex, 0, rows, cols, 1);
                    var objScore = ReadValue(obj, anchorIndex, 0, rows, cols, 1);
                    var score = MathF.Sqrt(Math.Clamp(clsScore, 0f, 1f) * Math.Clamp(objScore, 0f, 1f));
                    if (score < options.ScoreThreshold)
                    {
                        continue;
                    }

                    var x1 = (col - ReadValue(bbox, anchorIndex, 0, rows, cols, 4)) * stride;
                    var y1 = (row - ReadValue(bbox, anchorIndex, 1, rows, cols, 4)) * stride;
                    var x2 = (col + ReadValue(bbox, anchorIndex, 2, rows, cols, 4)) * stride;
                    var y2 = (row + ReadValue(bbox, anchorIndex, 3, rows, cols, 4)) * stride;

                    var mappedX1 = (x1 - prepared.OffsetX) / prepared.Scale;
                    var mappedY1 = (y1 - prepared.OffsetY) / prepared.Scale;
                    var mappedX2 = (x2 - prepared.OffsetX) / prepared.Scale;
                    var mappedY2 = (y2 - prepared.OffsetY) / prepared.Scale;

                    mappedX1 = Math.Clamp(mappedX1, 0f, imageWidth);
                    mappedY1 = Math.Clamp(mappedY1, 0f, imageHeight);
                    mappedX2 = Math.Clamp(mappedX2, 0f, imageWidth);
                    mappedY2 = Math.Clamp(mappedY2, 0f, imageHeight);

                    var width = mappedX2 - mappedX1;
                    var height = mappedY2 - mappedY1;
                    if (width <= 0 || height <= 0)
                    {
                        continue;
                    }

                    proposals.Add(new DetectionBox(mappedX1, mappedY1, width, height, score, "face"));
                }
            }
        }

        return proposals;
    }

    private static float ReadValue(TensorData tensor, int anchorIndex, int channel, int rows, int cols, int expectedChannels)
    {
        var shape = tensor.Shape;
        if (shape.Length == 1)
        {
            return tensor.Buffer[anchorIndex];
        }

        if (shape.Length == 2)
        {
            if (shape[1] == expectedChannels)
            {
                return tensor.Buffer[(anchorIndex * expectedChannels) + channel];
            }

            if (shape[0] == expectedChannels)
            {
                return tensor.Buffer[(channel * rows * cols) + anchorIndex];
            }
        }

        if (shape.Length == 3)
        {
            if (shape[2] == expectedChannels)
            {
                return tensor.Buffer[(anchorIndex * expectedChannels) + channel];
            }

            if (shape[0] == expectedChannels)
            {
                return tensor.Buffer[(channel * rows * cols) + anchorIndex];
            }

            if (shape[0] == 1)
            {
                return tensor.Buffer[anchorIndex];
            }
        }

        if (shape.Length == 4)
        {
            if (shape[3] == expectedChannels)
            {
                return tensor.Buffer[(anchorIndex * expectedChannels) + channel];
            }

            if (shape[1] == expectedChannels)
            {
                return tensor.Buffer[(channel * rows * cols) + anchorIndex];
            }

            if (shape[1] == 1 || shape[3] == 1)
            {
                return tensor.Buffer[anchorIndex];
            }
        }

        throw new InvalidOperationException($"Formato de tensor nao suportado: [{string.Join(",", shape)}]");
    }

    private static List<DetectionBox> ApplyNms(IEnumerable<DetectionBox> boxes, float threshold, int topK)
    {
        var ordered = boxes.OrderByDescending(box => box.Confidence).ToList();
        var selected = new List<DetectionBox>();
        foreach (var candidate in ordered)
        {
            if (selected.Count >= topK)
            {
                break;
            }

            var shouldKeep = true;
            foreach (var existing in selected)
            {
                if (IoU(candidate, existing) > threshold)
                {
                    shouldKeep = false;
                    break;
                }
            }

            if (shouldKeep)
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    private static float IoU(DetectionBox first, DetectionBox second)
    {
        var left = Math.Max(first.X, second.X);
        var top = Math.Max(first.Y, second.Y);
        var right = Math.Min(first.Right, second.Right);
        var bottom = Math.Min(first.Bottom, second.Bottom);
        var width = Math.Max(0f, right - left);
        var height = Math.Max(0f, bottom - top);
        var intersection = width * height;
        if (intersection <= 0)
        {
            return 0f;
        }

        var union = (first.Width * first.Height) + (second.Width * second.Height) - intersection;
        return union <= 0 ? 0f : intersection / union;
    }

    private static int AlignTo32(int value) => ((value + 31) / 32) * 32;

    public void Dispose()
    {
        foreach (var session in _sessions.Values)
        {
            session.Session.Dispose();
        }

        _sessions.Clear();
    }

    private sealed record DetectorSessionContext(InferenceSession Session, string InputName, int? InputWidth, int? InputHeight);

    private sealed record PreparedInput(
        Bitmap Bitmap,
        DenseTensor<float> Tensor,
        int ModelWidth,
        int ModelHeight,
        float Scale,
        float OffsetX,
        float OffsetY) : IDisposable
    {
        public void Dispose() => Bitmap.Dispose();
    }

    private sealed record TensorData(string Name, float[] Buffer, int[] Shape)
    {
        public static TensorData FromNamedValue(DisposableNamedOnnxValue value)
        {
            var tensor = value.AsTensor<float>();
            return new TensorData(value.Name, tensor.ToArray(), tensor.Dimensions.ToArray());
        }
    }
}