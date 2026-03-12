using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text.Json;
using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Helpers;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;
using Microsoft.Extensions.Logging;

namespace FaceCensorApp.Application.Services;

public sealed class BatchJobExecutor : IJobExecutor
{
    private readonly IMediaScanner _mediaScanner;
    private readonly IFaceDetector _faceDetector;
    private readonly IImageCensorService _imageCensorService;
    private readonly IVideoCensorService _videoCensorService;
    private readonly IOutputOrganizer _outputOrganizer;
    private readonly ILogService _logService;
    private readonly ILogger<BatchJobExecutor> _logger;

    public BatchJobExecutor(
        IMediaScanner mediaScanner,
        IFaceDetector faceDetector,
        IImageCensorService imageCensorService,
        IVideoCensorService videoCensorService,
        IOutputOrganizer outputOrganizer,
        ILogService logService,
        ILogger<BatchJobExecutor> logger)
    {
        _mediaScanner = mediaScanner;
        _faceDetector = faceDetector;
        _imageCensorService = imageCensorService;
        _videoCensorService = videoCensorService;
        _outputOrganizer = outputOrganizer;
        _logService = logService;
        _logger = logger;
    }

    public async Task<JobSummary> ExecuteAsync(
        ProcessingJob job,
        IProgress<JobProgress>? progress,
        Func<ReviewItem, CancellationToken, Task<ReviewDecision>>? reviewHandler,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(job.RootFolder);

        var jobWatch = Stopwatch.StartNew();
        var outputContext = await _outputOrganizer.InitializeAsync(job, cancellationToken);
        await _logService.InitializeAsync(outputContext.LogPath, cancellationToken);
        await _logService.WriteInfoAsync($"Execucao iniciada em {job.RootFolder}", cancellationToken);

        var scanResult = await _mediaScanner.ScanAsync(job.RootFolder, job.IncludeSubfolders, cancellationToken);
        var results = new List<ProcessingResult>();

        foreach (var video in scanResult.IgnoredVideos)
        {
            var ignored = new ProcessingResult(
                video.FullPath,
                null,
                false,
                0,
                Array.Empty<string>(),
                TimeSpan.Zero,
                ProcessingStatus.Ignored,
                video.MediaType,
                "Video ignorado na V1.");
            results.Add(ignored);
            await _logService.WriteWarningAsync($"Video ignorado: {video.RelativePath}", cancellationToken);
        }

        var totalImages = scanResult.Images.Count;
        progress?.Report(new JobProgress(0, totalImages, string.Empty, "Varredura concluida"));

        for (var index = 0; index < totalImages; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var mediaItem = scanResult.Images[index];
            var itemWatch = Stopwatch.StartNew();
            progress?.Report(new JobProgress(index, totalImages, mediaItem.RelativePath, "Processando imagem"));

            try
            {
                using var source = LoadBitmap(mediaItem.FullPath);
                var detectorOptions = new DetectorOptions(
                    ResolveModelPath(job),
                    ResolveDetectorThreshold(job.ConfidenceThreshold),
                    job.ConfidenceThreshold,
                    job.NmsThreshold,
                    job.TopK);

                var detections = (await _faceDetector.DetectAsync(source, detectorOptions, cancellationToken)).ToList();
                var reviewReason = ResolveReviewReason(detections, job.ConfidenceThreshold);
                IReadOnlyList<DetectionBox> finalBoxes;
                string? notes = null;

                if (reviewReason is not null)
                {
                    if (reviewHandler is null)
                    {
                        notes = "Item exige revisao manual, mas nenhum revisor foi configurado.";
                        results.Add(new ProcessingResult(
                            mediaItem.FullPath,
                            null,
                            false,
                            0,
                            Array.Empty<string>(),
                            itemWatch.Elapsed,
                            ProcessingStatus.Ignored,
                            mediaItem.MediaType,
                            notes));
                        await _logService.WriteWarningAsync($"Imagem ignorada por falta de revisor: {mediaItem.RelativePath}", cancellationToken);
                        continue;
                    }

                    var suggested = DetectionBoxHelper.ExpandAndClamp(detections, source.Width, source.Height, job.Preset.MarginPercent);
                    var reviewItem = new ReviewItem(
                        mediaItem,
                        reviewReason.Value,
                        suggested,
                        job.Preset,
                        job.ConfidenceThreshold,
                        BuildReviewMessage(reviewReason.Value));

                    progress?.Report(new JobProgress(index, totalImages, mediaItem.RelativePath, "Aguardando revisao manual"));
                    var decision = await reviewHandler(reviewItem, cancellationToken);
                    if (!decision.ProcessItem)
                    {
                        notes = decision.Notes ?? "Ignorado pelo revisor.";
                        results.Add(new ProcessingResult(
                            mediaItem.FullPath,
                            null,
                            false,
                            0,
                            Array.Empty<string>(),
                            itemWatch.Elapsed,
                            ProcessingStatus.Ignored,
                            mediaItem.MediaType,
                            notes));
                        await _logService.WriteWarningAsync($"Imagem ignorada na revisao: {mediaItem.RelativePath}", cancellationToken);
                        continue;
                    }

                    finalBoxes = decision.FinalBoxes;
                    notes = decision.Notes;
                }
                else
                {
                    finalBoxes = DetectionBoxHelper.ExpandAndClamp(detections, source.Width, source.Height, job.Preset.MarginPercent);
                }

                if (finalBoxes.Count == 0)
                {
                    notes ??= "Nenhuma mascara final definida para o arquivo.";
                    results.Add(new ProcessingResult(
                        mediaItem.FullPath,
                        null,
                        false,
                        0,
                        Array.Empty<string>(),
                        itemWatch.Elapsed,
                        ProcessingStatus.Ignored,
                        mediaItem.MediaType,
                        notes));
                    await _logService.WriteWarningAsync($"Imagem sem mascaras finais: {mediaItem.RelativePath}", cancellationToken);
                    continue;
                }

                using var censored = await _imageCensorService.ApplyAsync(source, finalBoxes, job.Preset, cancellationToken);
                var outputTarget = await _outputOrganizer.ResolveOutputAsync(outputContext, job, mediaItem, cancellationToken);
                await _outputOrganizer.BackupOriginalAsync(mediaItem, outputTarget, cancellationToken);
                SaveBitmap(censored, outputTarget.CensoredPath, mediaItem.Extension);

                var processed = new ProcessingResult(
                    mediaItem.FullPath,
                    outputTarget.CensoredPath,
                    true,
                    finalBoxes.Count,
                    Array.Empty<string>(),
                    itemWatch.Elapsed,
                    ProcessingStatus.Processed,
                    mediaItem.MediaType,
                    notes);
                results.Add(processed);
                await _logService.WriteInfoAsync($"Arquivo processado: {mediaItem.RelativePath}", cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao processar {RelativePath}", mediaItem.RelativePath);
                await _logService.WriteErrorAsync($"Falha ao processar {mediaItem.RelativePath}", ex, cancellationToken);
                results.Add(new ProcessingResult(
                    mediaItem.FullPath,
                    null,
                    false,
                    0,
                    new[] { ex.Message },
                    itemWatch.Elapsed,
                    ProcessingStatus.Failed,
                    mediaItem.MediaType,
                    "Erro no processamento do arquivo."));
            }
            finally
            {
                progress?.Report(new JobProgress(index + 1, totalImages, mediaItem.RelativePath, "Item concluido"));
            }
        }

        jobWatch.Stop();
        var summary = new JobSummary(
            outputContext.RunId,
            outputContext.RunRoot,
            outputContext.SummaryPath,
            outputContext.LogPath,
            jobWatch.Elapsed,
            results);

        var json = JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(outputContext.SummaryPath, json, cancellationToken);
        await _logService.WriteInfoAsync("Execucao finalizada.", cancellationToken);
        return summary;
    }

    private static Bitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        using var original = new Bitmap(stream);
        return new Bitmap(original);
    }

    private static string ResolveModelPath(ProcessingJob job)
    {
        if (!string.IsNullOrWhiteSpace(job.ModelPath))
        {
            return job.ModelPath;
        }

        return Path.Combine(AppContext.BaseDirectory, "assets", "models", "face_detection_yunet_2023mar.onnx");
    }

    private static float ResolveDetectorThreshold(float confidenceThreshold) =>
        Math.Clamp(confidenceThreshold * 0.6f, 0.15f, 0.45f);

    private static ReviewReason? ResolveReviewReason(IReadOnlyList<DetectionBox> detections, float reviewThreshold)
    {
        if (detections.Count == 0)
        {
            return ReviewReason.NoFacesDetected;
        }

        return detections.Any(detection => detection.Confidence < reviewThreshold)
            ? ReviewReason.LowConfidenceDetections
            : null;
    }

    private static string BuildReviewMessage(ReviewReason reviewReason) => reviewReason switch
    {
        ReviewReason.NoFacesDetected => "Nenhum rosto foi detectado automaticamente. Adicione mascaras manuais para seguir.",
        ReviewReason.LowConfidenceDetections => "Foram encontradas deteccoes abaixo do limiar configurado. Revise as mascaras antes de salvar.",
        _ => "Revise o item antes do processamento."
    };

    private static void SaveBitmap(Bitmap bitmap, string outputPath, string extension)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var format = extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => ImageFormat.Jpeg,
            ".bmp" => ImageFormat.Bmp,
            _ => ImageFormat.Png
        };

        bitmap.Save(outputPath, format);
    }
}