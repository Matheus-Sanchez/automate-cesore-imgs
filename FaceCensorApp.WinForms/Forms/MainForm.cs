using System.Diagnostics;
using System.Drawing;
using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Helpers;
using FaceCensorApp.Application.Models;
using FaceCensorApp.Domain.Enums;
using FaceCensorApp.Domain.Models;
using FaceCensorApp.WinForms.Controls;
using FaceCensorApp.WinForms.Models;
using Microsoft.Extensions.Logging;

namespace FaceCensorApp.WinForms.Forms;

public sealed class MainForm : Form
{
    private readonly IMediaScanner _mediaScanner;
    private readonly IFaceDetector _faceDetector;
    private readonly IImageCensorService _imageCensorService;
    private readonly IJobExecutor _jobExecutor;
    private readonly ISettingsRepository _settingsRepository;
    private readonly ILogger<MainForm> _logger;
    private readonly TextBox _rootFolderTextBox = new() { Dock = DockStyle.Fill };
    private readonly TextBox _modelPathTextBox = new() { Dock = DockStyle.Fill };
    private readonly ComboBox _filterComboBox = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly Button _filterColorButton = new() { Text = "Cor", AutoSize = true, BackColor = Color.Black, ForeColor = Color.White };
    private readonly NumericUpDown _confidenceNumeric = new() { Width = 90, DecimalPlaces = 2, Increment = 0.05m, Minimum = 0.15m, Maximum = 0.99m, Value = 0.60m };
    private readonly NumericUpDown _marginNumeric = new() { Width = 90, DecimalPlaces = 0, Minimum = 0, Maximum = 100, Value = 15 };
    private readonly NumericUpDown _blurNumeric = new() { Width = 90, DecimalPlaces = 0, Minimum = 2, Maximum = 24, Value = 8 };
    private readonly NumericUpDown _pixelNumeric = new() { Width = 90, DecimalPlaces = 0, Minimum = 2, Maximum = 48, Value = 12 };
    private readonly NumericUpDown _opacityNumeric = new() { Width = 90, DecimalPlaces = 0, Minimum = 10, Maximum = 100, Value = 100 };
    private readonly CheckBox _keepOriginalsCheckBox = new() { Text = "Copiar originais para a saida", AutoSize = true };
    private readonly CheckBox _overwriteCheckBox = new() { Text = "Sobrescrever arquivos de origem", AutoSize = true };
    private readonly CheckBox _backupCheckBox = new() { Text = "Criar backup ao sobrescrever", AutoSize = true, Checked = true };
    private readonly Button _browseFolderButton = new() { Text = "Selecionar pasta", AutoSize = true };
    private readonly Button _refreshListButton = new() { Text = "Atualizar lista", AutoSize = true };
    private readonly Button _browseModelButton = new() { Text = "Selecionar modelo", AutoSize = true };
    private readonly Button _refreshPreviewButton = new() { Text = "Atualizar pre-visualizacao", AutoSize = true };
    private readonly Button _addMaskButton = new() { Text = "Adicionar mascara", AutoSize = true };
    private readonly Button _removeMaskButton = new() { Text = "Remover selecionada", AutoSize = true };
    private readonly Button _processButton = new() { Text = "Processar lote", AutoSize = true };
    private readonly Button _openOutputButton = new() { Text = "Abrir pasta de saida", AutoSize = true, Enabled = false };
    private readonly ListBox _fileList = new() { Dock = DockStyle.Fill };
    private readonly Label _scanInfoLabel = new() { Dock = DockStyle.Fill, Padding = new Padding(8) };
    private readonly Label _statusLabel = new() { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 8, 4), Text = "Pronto." };
    private readonly ProgressBar _progressBar = new() { Dock = DockStyle.Fill, Minimum = 0, Maximum = 100 };
    private readonly TextBox _summaryTextBox = new() { Dock = DockStyle.Fill, Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical };
    private readonly ImageCanvasControl _previewCanvas = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _previewBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 28, 28) };
    private MediaScanResult _scanResult = new(Array.Empty<MediaItem>(), Array.Empty<MediaItem>(), Array.Empty<string>());
    private Bitmap? _currentSampleBitmap;
    private Bitmap? _currentPreviewBitmap;
    private JobSummary? _lastSummary;

    public MainForm(
        IMediaScanner mediaScanner,
        IFaceDetector faceDetector,
        IImageCensorService imageCensorService,
        IJobExecutor jobExecutor,
        ISettingsRepository settingsRepository,
        ILogger<MainForm> logger)
    {
        _mediaScanner = mediaScanner;
        _faceDetector = faceDetector;
        _imageCensorService = imageCensorService;
        _jobExecutor = jobExecutor;
        _settingsRepository = settingsRepository;
        _logger = logger;

        Text = "FaceCensorApp";
        Width = 1500;
        Height = 950;
        StartPosition = FormStartPosition.CenterScreen;
        BuildLayout();
        WireEvents();
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);
        var settings = await _settingsRepository.LoadAsync(CancellationToken.None);
        _rootFolderTextBox.Text = settings.LastRootFolder ?? string.Empty;
        _modelPathTextBox.Text = settings.LastModelPath ?? ResolveDefaultModelPath();
        _confidenceNumeric.Value = (decimal)settings.DefaultConfidenceThreshold;
        ApplyPreset(settings.DefaultPreset);
        UpdateFilterControlStates();
        UpdateOverwriteOptions();
        if (Directory.Exists(_rootFolderTextBox.Text))
        {
            await RefreshScanAsync();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        var settings = new AppSettings
        {
            LastRootFolder = _rootFolderTextBox.Text,
            LastModelPath = _modelPathTextBox.Text,
            DefaultConfidenceThreshold = (float)_confidenceNumeric.Value,
            DefaultPreset = BuildPreset()
        };

        _settingsRepository.SaveAsync(settings, CancellationToken.None).GetAwaiter().GetResult();
        _currentPreviewBitmap?.Dispose();
        _currentSampleBitmap?.Dispose();
    }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 220f));

        root.Controls.Add(BuildConfigurationPanel(), 0, 0);
        root.Controls.Add(BuildContentPanel(), 0, 1);
        root.Controls.Add(BuildStatusPanel(), 0, 2);
        Controls.Add(root);
    }

    private Control BuildConfigurationPanel()
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(8),
            AutoSize = true
        };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var paths = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 7, AutoSize = true };
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        paths.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        paths.Controls.Add(new Label { Text = "Pasta raiz", Anchor = AnchorStyles.Left, AutoSize = true }, 0, 0);
        paths.Controls.Add(_rootFolderTextBox, 1, 0);
        paths.Controls.Add(_browseFolderButton, 2, 0);
        paths.Controls.Add(_refreshListButton, 3, 0);
        paths.Controls.Add(new Label { Text = "Modelo ONNX", Anchor = AnchorStyles.Left, AutoSize = true }, 4, 0);
        paths.Controls.Add(_modelPathTextBox, 5, 0);
        paths.Controls.Add(_browseModelButton, 6, 0);

        var filters = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        filters.Controls.Add(new Label { Text = "Filtro", AutoSize = true, Padding = new Padding(0, 8, 0, 0) });
        filters.Controls.Add(_filterComboBox);
        filters.Controls.Add(_filterColorButton);
        filters.Controls.Add(new Label { Text = "Confianca", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filters.Controls.Add(_confidenceNumeric);
        filters.Controls.Add(new Label { Text = "Margem (%)", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filters.Controls.Add(_marginNumeric);
        filters.Controls.Add(new Label { Text = "Blur", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filters.Controls.Add(_blurNumeric);
        filters.Controls.Add(new Label { Text = "Pixel", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filters.Controls.Add(_pixelNumeric);
        filters.Controls.Add(new Label { Text = "Opacidade (%)", AutoSize = true, Padding = new Padding(12, 8, 0, 0) });
        filters.Controls.Add(_opacityNumeric);

        var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        actions.Controls.Add(_keepOriginalsCheckBox);
        actions.Controls.Add(_overwriteCheckBox);
        actions.Controls.Add(_backupCheckBox);
        actions.Controls.Add(_refreshPreviewButton);
        actions.Controls.Add(_addMaskButton);
        actions.Controls.Add(_removeMaskButton);
        actions.Controls.Add(_processButton);
        actions.Controls.Add(_openOutputButton);

        panel.Controls.Add(paths, 0, 0);
        panel.Controls.Add(filters, 0, 1);
        panel.Controls.Add(actions, 0, 2);
        return panel;
    }

    private Control BuildContentPanel()
    {
        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 360 };
        var listLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        listLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        listLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        listLayout.Controls.Add(new Label { Text = "Arquivos de imagem encontrados", Dock = DockStyle.Fill, Padding = new Padding(8), AutoSize = true }, 0, 0);
        listLayout.Controls.Add(_fileList, 0, 1);
        listLayout.Controls.Add(_scanInfoLabel, 0, 2);
        split.Panel1.Controls.Add(listLayout);

        var previewSplit = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 540 };
        previewSplit.Panel1.Controls.Add(BuildPreviewPane("Mascaras editaveis", _previewCanvas));
        previewSplit.Panel2.Controls.Add(BuildPreviewPane("Resultado da censura", _previewBox));
        split.Panel2.Controls.Add(previewSplit);
        return split;
    }

    private Control BuildStatusPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(8) };
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        panel.Controls.Add(_statusLabel, 0, 0);
        panel.Controls.Add(_progressBar, 0, 1);
        panel.Controls.Add(_summaryTextBox, 0, 2);
        return panel;
    }

    private static Control BuildPreviewPane(string title, Control child)
    {
        var group = new GroupBox { Dock = DockStyle.Fill, Text = title };
        group.Controls.Add(child);
        return group;
    }

    private void WireEvents()
    {
        _filterComboBox.DataSource = Enum.GetValues(typeof(FilterType));
        _filterComboBox.Format += (_, e) => e.Value = GetFilterLabel((FilterType)e.Value!);
        _browseFolderButton.Click += async (_, _) => await SelectFolderAsync();
        _browseModelButton.Click += (_, _) => SelectModel();
        _refreshListButton.Click += async (_, _) => await RefreshScanAsync();
        _refreshPreviewButton.Click += async (_, _) => await RefreshPreviewAsync();
        _fileList.SelectedIndexChanged += async (_, _) => await LoadSelectedPreviewAsync();
        _filterColorButton.Click += (_, _) => SelectColor();
        _addMaskButton.Click += (_, _) => _previewCanvas.BeginAddBox();
        _removeMaskButton.Click += async (_, _) =>
        {
            _previewCanvas.RemoveSelectedBox();
            await RenderPreviewAsync();
        };
        _processButton.Click += async (_, _) => await ProcessBatchAsync();
        _openOutputButton.Click += (_, _) => OpenLastOutput();
        _overwriteCheckBox.CheckedChanged += (_, _) => UpdateOverwriteOptions();
        _filterComboBox.SelectedIndexChanged += (_, _) => UpdateFilterControlStates();
        _filterComboBox.SelectedIndexChanged += async (_, _) => await RenderPreviewAsync();
        _marginNumeric.ValueChanged += async (_, _) => await RefreshPreviewAsync();
        _confidenceNumeric.ValueChanged += async (_, _) => await RefreshPreviewAsync();
        _blurNumeric.ValueChanged += async (_, _) => await RenderPreviewAsync();
        _pixelNumeric.ValueChanged += async (_, _) => await RenderPreviewAsync();
        _opacityNumeric.ValueChanged += async (_, _) => await RenderPreviewAsync();
        _previewCanvas.BoxesChanged += async (_, _) => await RenderPreviewAsync();
    }

    private async Task SelectFolderAsync()
    {
        using var dialog = new FolderBrowserDialog { Description = "Selecione a pasta raiz para varrer imagens." };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _rootFolderTextBox.Text = dialog.SelectedPath;
            await RefreshScanAsync();
        }
    }

    private void SelectModel()
    {
        using var dialog = new OpenFileDialog
        {
            Filter = "Modelos ONNX (*.onnx)|*.onnx|Todos os arquivos (*.*)|*.*",
            FileName = Path.GetFileName(ResolveDefaultModelPath())
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _modelPathTextBox.Text = dialog.FileName;
        }
    }

    private void SelectColor()
    {
        using var dialog = new ColorDialog { Color = _filterColorButton.BackColor };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _filterColorButton.BackColor = dialog.Color;
            _filterColorButton.ForeColor = dialog.Color.GetBrightness() > 0.5f ? Color.Black : Color.White;
            _ = RenderPreviewAsync();
        }
    }

    private async Task RefreshScanAsync()
    {
        var rootFolder = _rootFolderTextBox.Text.Trim();
        if (!Directory.Exists(rootFolder))
        {
            SetStatus("Selecione uma pasta raiz valida.");
            return;
        }

        try
        {
            UseWaitCursor = true;
            _scanResult = await _mediaScanner.ScanAsync(rootFolder, includeSubfolders: true, CancellationToken.None);
            _fileList.DisplayMember = nameof(MediaItem.RelativePath);
            _fileList.DataSource = _scanResult.Images.ToList();
            _scanInfoLabel.Text = $"Imagens: {_scanResult.Images.Count} | Videos ignorados: {_scanResult.IgnoredVideos.Count} | Outros arquivos: {_scanResult.UnsupportedFiles.Count}";
            _summaryTextBox.Text = $"Varredura concluida em {rootFolder}{Environment.NewLine}" +
                $"Imagens prontas para processamento: {_scanResult.Images.Count}{Environment.NewLine}" +
                $"Videos ignorados na V1: {_scanResult.IgnoredVideos.Count}{Environment.NewLine}" +
                $"Arquivos sem suporte: {_scanResult.UnsupportedFiles.Count}";
            SetStatus("Varredura concluida.");
            if (_scanResult.Images.Count > 0)
            {
                _fileList.SelectedIndex = 0;
            }
            else
            {
                ClearPreview();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na varredura inicial.");
            SetStatus("Falha ao varrer a pasta selecionada.");
            MessageBox.Show(this, ex.Message, "Erro na varredura", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            UseWaitCursor = false;
        }
    }

    private async Task LoadSelectedPreviewAsync()
    {
        if (_fileList.SelectedItem is not MediaItem item)
        {
            ClearPreview();
            return;
        }

        _currentSampleBitmap?.Dispose();
        _currentSampleBitmap = LoadBitmap(item.FullPath);
        _previewCanvas.SetImage(_currentSampleBitmap);
        await RefreshPreviewAsync();
    }

    private async Task RefreshPreviewAsync()
    {
        if (_currentSampleBitmap is null)
        {
            return;
        }

        try
        {
            var modelPath = string.IsNullOrWhiteSpace(_modelPathTextBox.Text) ? ResolveDefaultModelPath() : _modelPathTextBox.Text.Trim();
            if (!File.Exists(modelPath))
            {
                SetStatus("Modelo ONNX nao encontrado. Selecione um arquivo valido para habilitar a deteccao automatica.");
                _previewCanvas.SetBoxes(Array.Empty<EditableDetectionBox>());
                await RenderPreviewAsync();
                return;
            }

            var detectorOptions = new DetectorOptions(
                modelPath,
                Math.Clamp((float)_confidenceNumeric.Value * 0.6f, 0.15f, 0.45f),
                (float)_confidenceNumeric.Value,
                0.30f,
                5000);
            var detections = await _faceDetector.DetectAsync(_currentSampleBitmap, detectorOptions, CancellationToken.None);
            var expanded = DetectionBoxHelper.ExpandAndClamp(detections, _currentSampleBitmap.Width, _currentSampleBitmap.Height, (float)_marginNumeric.Value)
                .Select(box => new EditableDetectionBox(box, false))
                .ToList();
            _previewCanvas.SetBoxes(expanded);
            SetStatus($"Previa atualizada com {expanded.Count} mascaras.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao atualizar a previa.");
            SetStatus("Falha ao detectar rostos na amostra selecionada.");
            MessageBox.Show(this, ex.Message, "Erro na previa", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private async Task RenderPreviewAsync()
    {
        if (_currentSampleBitmap is null)
        {
            _previewBox.Image = null;
            return;
        }

        _currentPreviewBitmap?.Dispose();
        _currentPreviewBitmap = await _imageCensorService.ApplyAsync(_currentSampleBitmap, _previewCanvas.GetDetectionBoxes(), BuildPreset(), CancellationToken.None);
        _previewBox.Image = _currentPreviewBitmap;
    }

    private async Task ProcessBatchAsync()
    {
        if (!Directory.Exists(_rootFolderTextBox.Text.Trim()))
        {
            MessageBox.Show(this, "Selecione uma pasta raiz valida antes de iniciar o processamento.", "Pasta obrigatoria", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_scanResult.Images.Count == 0)
        {
            MessageBox.Show(this, "Nenhuma imagem suportada foi encontrada para processamento.", "Nada para processar", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_overwriteCheckBox.Checked)
        {
            var confirm = MessageBox.Show(
                this,
                "O modo de sobrescrita altera os arquivos originais. Confirma a execucao?",
                "Confirmar sobrescrita",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (confirm != DialogResult.Yes)
            {
                return;
            }
        }

        try
        {
            ApplyProcessingState(true);
            _summaryTextBox.Text = "Processamento iniciado...";
            var job = BuildJob();
            var progress = new Progress<JobProgress>(progressInfo =>
            {
                _progressBar.Maximum = Math.Max(1, progressInfo.TotalItems);
                _progressBar.Value = Math.Min(_progressBar.Maximum, Math.Max(0, progressInfo.CurrentIndex));
                SetStatus($"{progressInfo.StatusText}: {progressInfo.CurrentItem}");
            });

            _lastSummary = await Task.Run(async () => await _jobExecutor.ExecuteAsync(job, progress, ReviewAsync, CancellationToken.None));
            _openOutputButton.Enabled = !string.IsNullOrWhiteSpace(_lastSummary.RunRoot);
            _summaryTextBox.Text =
                $"Execucao concluida.{Environment.NewLine}" +
                $"Processados: {_lastSummary.ProcessedCount}{Environment.NewLine}" +
                $"Ignorados: {_lastSummary.IgnoredCount}{Environment.NewLine}" +
                $"Falhas: {_lastSummary.FailedCount}{Environment.NewLine}" +
                $"Rostos detectados: {_lastSummary.TotalFacesDetected}{Environment.NewLine}" +
                $"Duracao: {_lastSummary.Duration}{Environment.NewLine}" +
                $"Saida: {_lastSummary.RunRoot}{Environment.NewLine}" +
                $"Resumo JSON: {_lastSummary.SummaryPath}";
            SetStatus("Processamento concluido.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha no processamento do lote.");
            SetStatus("Falha ao processar o lote.");
            MessageBox.Show(this, ex.Message, "Erro no processamento", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            ApplyProcessingState(false);
        }
    }

    private Task<ReviewDecision> ReviewAsync(ReviewItem item, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<ReviewDecision>();
        BeginInvoke(new Action(() =>
        {
            try
            {
                using var form = new ReviewForm(item, _imageCensorService);
                var result = form.ShowDialog(this);
                completion.TrySetResult(result == DialogResult.OK ? form.Decision : ReviewDecision.Ignore("Revisao cancelada."));
            }
            catch (Exception ex)
            {
                completion.TrySetException(ex);
            }
        }));
        return completion.Task;
    }

    private void OpenLastOutput()
    {
        if (string.IsNullOrWhiteSpace(_lastSummary?.RunRoot) || !Directory.Exists(_lastSummary.RunRoot))
        {
            return;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = _lastSummary.RunRoot,
            UseShellExecute = true
        });
    }

    private void ApplyProcessingState(bool isRunning)
    {
        UseWaitCursor = isRunning;
        _processButton.Enabled = !isRunning;
        _refreshListButton.Enabled = !isRunning;
        _refreshPreviewButton.Enabled = !isRunning;
        _browseFolderButton.Enabled = !isRunning;
        _browseModelButton.Enabled = !isRunning;
    }

    private void UpdateOverwriteOptions()
    {
        _backupCheckBox.Enabled = _overwriteCheckBox.Checked;
        if (!_overwriteCheckBox.Checked)
        {
            _backupCheckBox.Checked = true;
        }
    }

    private void UpdateFilterControlStates()
    {
        var filter = (FilterType)_filterComboBox.SelectedItem;
        _filterColorButton.Enabled = filter is FilterType.BlackCircle or FilterType.SolidRectangle;
        _blurNumeric.Enabled = filter == FilterType.Blur;
        _pixelNumeric.Enabled = filter == FilterType.Pixelate;
    }

    private ProcessingJob BuildJob()
    {
        var modelPath = string.IsNullOrWhiteSpace(_modelPathTextBox.Text) ? ResolveDefaultModelPath() : _modelPathTextBox.Text.Trim();
        return new ProcessingJob
        {
            RootFolder = _rootFolderTextBox.Text.Trim(),
            KeepOriginals = _keepOriginalsCheckBox.Checked,
            OutputMode = _overwriteCheckBox.Checked ? OutputMode.OverwriteInPlace : OutputMode.SeparateOutput,
            CreateBackupWhenOverwriting = _backupCheckBox.Checked,
            Preset = BuildPreset(),
            ConfidenceThreshold = (float)_confidenceNumeric.Value,
            ModelPath = modelPath
        };
    }

    private CensorPreset BuildPreset() => new(
        (FilterType)_filterComboBox.SelectedItem,
        _filterColorButton.BackColor.ToArgb(),
        (float)_marginNumeric.Value,
        (int)_blurNumeric.Value,
        (int)_pixelNumeric.Value,
        (float)_opacityNumeric.Value / 100f);

    private void ApplyPreset(CensorPreset preset)
    {
        _filterComboBox.SelectedItem = preset.FilterType;
        _filterColorButton.BackColor = Color.FromArgb(preset.ColorArgb);
        _filterColorButton.ForeColor = _filterColorButton.BackColor.GetBrightness() > 0.5f ? Color.Black : Color.White;
        _marginNumeric.Value = (decimal)Math.Clamp(preset.MarginPercent, (float)_marginNumeric.Minimum, (float)_marginNumeric.Maximum);
        _blurNumeric.Value = (decimal)Math.Clamp(preset.BlurLevel, (int)_blurNumeric.Minimum, (int)_blurNumeric.Maximum);
        _pixelNumeric.Value = (decimal)Math.Clamp(preset.PixelBlockSize, (int)_pixelNumeric.Minimum, (int)_pixelNumeric.Maximum);
        _opacityNumeric.Value = (decimal)Math.Clamp(preset.Opacity * 100f, (float)_opacityNumeric.Minimum, (float)_opacityNumeric.Maximum);
    }

    private static string GetFilterLabel(FilterType filterType) => filterType switch
    {
        FilterType.BlackCircle => "Circulo preto",
        FilterType.SolidRectangle => "Retangulo solido",
        FilterType.Pixelate => "Pixelizacao",
        FilterType.Blur => "Desfoque simples",
        _ => filterType.ToString()
    };

    private string ResolveDefaultModelPath() =>
        Path.Combine(AppContext.BaseDirectory, "assets", "models", "face_detection_yunet_2023mar.onnx");

    private void ClearPreview()
    {
        _currentSampleBitmap?.Dispose();
        _currentSampleBitmap = null;
        _currentPreviewBitmap?.Dispose();
        _currentPreviewBitmap = null;
        _previewCanvas.SetImage(null);
        _previewCanvas.SetBoxes(Array.Empty<EditableDetectionBox>());
        _previewBox.Image = null;
    }

    private void SetStatus(string text) => _statusLabel.Text = text;

    private static Bitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        using var original = new Bitmap(stream);
        return new Bitmap(original);
    }
}