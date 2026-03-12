using System.Drawing;
using FaceCensorApp.Application.Contracts;
using FaceCensorApp.Application.Helpers;
using FaceCensorApp.Domain.Models;
using FaceCensorApp.WinForms.Controls;
using FaceCensorApp.WinForms.Models;

namespace FaceCensorApp.WinForms.Forms;

public sealed class ReviewForm : Form
{
    private readonly ReviewItem _item;
    private readonly IImageCensorService _imageCensorService;
    private readonly ImageCanvasControl _canvas = new() { Dock = DockStyle.Fill };
    private readonly PictureBox _previewBox = new() { Dock = DockStyle.Fill, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.FromArgb(28, 28, 28) };
    private readonly Label _messageLabel = new() { Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(8) };
    private readonly NumericUpDown _extraMarginNumeric = new() { Width = 80, DecimalPlaces = 0, Minimum = -50, Maximum = 100, Value = 0 };
    private Bitmap? _sourceImage;
    private Bitmap? _previewImage;

    public ReviewForm(ReviewItem item, IImageCensorService imageCensorService)
    {
        _item = item;
        _imageCensorService = imageCensorService;
        Decision = ReviewDecision.Ignore("Revisao cancelada.");
        Text = $"Revisao de excecao - {item.MediaItem.FileName}";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterParent;
        BuildLayout();
        Load += ReviewForm_Load;
    }

    public ReviewDecision Decision { get; private set; }

    private void BuildLayout()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _messageLabel.Text = $"{_item.Message}{Environment.NewLine}{_item.MediaItem.RelativePath}";
        root.Controls.Add(_messageLabel, 0, 0);

        var split = new SplitContainer { Dock = DockStyle.Fill, SplitterDistance = 600 };
        split.Panel1.Controls.Add(_canvas);
        split.Panel2.Controls.Add(_previewBox);
        root.Controls.Add(split, 0, 1);

        var actions = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            Padding = new Padding(8)
        };

        var addMaskButton = new Button { Text = "Adicionar mascara", AutoSize = true };
        addMaskButton.Click += (_, _) => _canvas.BeginAddBox();
        var removeMaskButton = new Button { Text = "Remover selecionada", AutoSize = true };
        removeMaskButton.Click += (_, _) =>
        {
            _canvas.RemoveSelectedBox();
            _ = RenderPreviewAsync();
        };
        var applyMarginButton = new Button { Text = "Aplicar margem", AutoSize = true };
        applyMarginButton.Click += (_, _) =>
        {
            ApplyExtraMargin();
            _ = RenderPreviewAsync();
        };
        var processButton = new Button { Text = "Salvar e processar", AutoSize = true };
        processButton.Click += (_, _) =>
        {
            Decision = ReviewDecision.Process(_canvas.GetDetectionBoxes(), "Revisado manualmente.");
            DialogResult = DialogResult.OK;
            Close();
        };
        var ignoreButton = new Button { Text = "Ignorar arquivo", AutoSize = true };
        ignoreButton.Click += (_, _) =>
        {
            Decision = ReviewDecision.Ignore("Ignorado na revisao manual.");
            DialogResult = DialogResult.Cancel;
            Close();
        };

        actions.Controls.Add(addMaskButton);
        actions.Controls.Add(removeMaskButton);
        actions.Controls.Add(new Label { Text = "Margem extra (%)", AutoSize = true, TextAlign = ContentAlignment.MiddleLeft, Padding = new Padding(8, 8, 0, 0) });
        actions.Controls.Add(_extraMarginNumeric);
        actions.Controls.Add(applyMarginButton);
        actions.Controls.Add(processButton);
        actions.Controls.Add(ignoreButton);
        root.Controls.Add(actions, 0, 2);

        Controls.Add(root);
        _canvas.BoxesChanged += async (_, _) => await RenderPreviewAsync();
    }

    private async void ReviewForm_Load(object? sender, EventArgs e)
    {
        _sourceImage = LoadBitmap(_item.MediaItem.FullPath);
        _canvas.SetImage(_sourceImage);
        _canvas.SetBoxes(_item.SuggestedBoxes.Select(box => new EditableDetectionBox(box, false)));
        await RenderPreviewAsync();
    }

    private void ApplyExtraMargin()
    {
        if (_sourceImage is null)
        {
            return;
        }

        var adjusted = _canvas.GetBoxes()
            .Select(box => new EditableDetectionBox(
                DetectionBoxHelper.ExpandAndClamp(box.Box, _sourceImage.Width, _sourceImage.Height, (float)_extraMarginNumeric.Value),
                box.IsManual))
            .ToList();
        _canvas.SetBoxes(adjusted);
    }

    private async Task RenderPreviewAsync()
    {
        if (_sourceImage is null)
        {
            return;
        }

        _previewImage?.Dispose();
        _previewImage = await _imageCensorService.ApplyAsync(_sourceImage, _canvas.GetDetectionBoxes(), _item.Preset, CancellationToken.None);
        _previewBox.Image = _previewImage;
    }

    private static Bitmap LoadBitmap(string path)
    {
        using var stream = File.OpenRead(path);
        using var original = new Bitmap(stream);
        return new Bitmap(original);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _previewImage?.Dispose();
            _sourceImage?.Dispose();
        }

        base.Dispose(disposing);
    }
}