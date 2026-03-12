using System.Drawing.Drawing2D;
using FaceCensorApp.Domain.Models;
using FaceCensorApp.WinForms.Models;

namespace FaceCensorApp.WinForms.Controls;

public sealed class ImageCanvasControl : Control
{
    private readonly List<EditableDetectionBox> _boxes = [];
    private Bitmap? _image;
    private int _selectedIndex = -1;
    private bool _addMode;
    private PointF? _dragStartImage;
    private RectangleF? _draftRect;

    public ImageCanvasControl()
    {
        DoubleBuffered = true;
        ResizeRedraw = true;
        BackColor = Color.FromArgb(28, 28, 28);
    }

    public event EventHandler? BoxesChanged;

    public event EventHandler? SelectedBoxChanged;

    public int SelectedIndex => _selectedIndex;

    public void SetImage(Bitmap? image)
    {
        _image = image;
        _selectedIndex = -1;
        _draftRect = null;
        Invalidate();
    }

    public void SetBoxes(IEnumerable<EditableDetectionBox> boxes)
    {
        _boxes.Clear();
        _boxes.AddRange(boxes.Select(box => box.Clone()));
        _selectedIndex = -1;
        Invalidate();
        BoxesChanged?.Invoke(this, EventArgs.Empty);
    }

    public IReadOnlyList<EditableDetectionBox> GetBoxes() => _boxes.Select(box => box.Clone()).ToList();

    public IReadOnlyList<DetectionBox> GetDetectionBoxes() => _boxes.Select(box => box.Clone().Box).ToList();

    public void BeginAddBox()
    {
        _addMode = true;
        Cursor = Cursors.Cross;
    }

    public void RemoveSelectedBox()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _boxes.Count)
        {
            return;
        }

        _boxes.RemoveAt(_selectedIndex);
        _selectedIndex = -1;
        Invalidate();
        BoxesChanged?.Invoke(this, EventArgs.Empty);
        SelectedBoxChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (_image is null)
        {
            return;
        }

        if (_addMode)
        {
            if (TryMapClientToImage(e.Location, out var imagePoint))
            {
                _dragStartImage = imagePoint;
                _draftRect = new RectangleF(imagePoint.X, imagePoint.Y, 0, 0);
                Invalidate();
            }

            return;
        }

        _selectedIndex = HitTest(e.Location);
        Invalidate();
        SelectedBoxChanged?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (!_addMode || _dragStartImage is null)
        {
            return;
        }

        if (!TryMapClientToImage(e.Location, out var current))
        {
            current = _dragStartImage.Value;
        }

        var x = Math.Min(_dragStartImage.Value.X, current.X);
        var y = Math.Min(_dragStartImage.Value.Y, current.Y);
        var width = Math.Abs(current.X - _dragStartImage.Value.X);
        var height = Math.Abs(current.Y - _dragStartImage.Value.Y);
        _draftRect = new RectangleF(x, y, width, height);
        Invalidate();
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        if (!_addMode || _dragStartImage is null || _draftRect is null)
        {
            return;
        }

        var draft = _draftRect.Value;
        _dragStartImage = null;
        _draftRect = null;
        _addMode = false;
        Cursor = Cursors.Default;

        if (draft.Width >= 4 && draft.Height >= 4)
        {
            _boxes.Add(new EditableDetectionBox(new DetectionBox(draft.X, draft.Y, draft.Width, draft.Height, 1f, "manual"), true));
            _selectedIndex = _boxes.Count - 1;
            BoxesChanged?.Invoke(this, EventArgs.Empty);
            SelectedBoxChanged?.Invoke(this, EventArgs.Empty);
        }

        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        e.Graphics.Clear(BackColor);
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;

        if (_image is null)
        {
            using var brush = new SolidBrush(Color.Gainsboro);
            var format = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            e.Graphics.DrawString("Selecione uma imagem para visualizar.", Font, brush, ClientRectangle, format);
            return;
        }

        var layout = GetImageLayout(_image.Size);
        e.Graphics.DrawImage(_image, layout);

        for (var index = 0; index < _boxes.Count; index++)
        {
            var box = _boxes[index];
            var rect = MapImageRectToClient(box.Box, layout, _image.Size);
            var isSelected = index == _selectedIndex;
            using var pen = new Pen(isSelected ? Color.LawnGreen : box.IsManual ? Color.Orange : Color.DeepSkyBlue, isSelected ? 3f : 2f);
            e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);

            var text = box.IsManual ? "manual" : $"{box.Box.Confidence:0.00}";
            using var brush = new SolidBrush(Color.FromArgb(190, 0, 0, 0));
            var textSize = e.Graphics.MeasureString(text, Font);
            var labelRect = new RectangleF(rect.X, Math.Max(0, rect.Y - textSize.Height), textSize.Width + 8, textSize.Height + 2);
            e.Graphics.FillRectangle(brush, labelRect);
            using var textBrush = new SolidBrush(Color.WhiteSmoke);
            e.Graphics.DrawString(text, Font, textBrush, labelRect.X + 4, labelRect.Y + 1);
        }

        if (_draftRect is RectangleF draftRect)
        {
            var rect = MapImageRectToClient(new DetectionBox(draftRect.X, draftRect.Y, draftRect.Width, draftRect.Height, 1f), layout, _image.Size);
            using var pen = new Pen(Color.WhiteSmoke, 2f) { DashStyle = DashStyle.Dash };
            e.Graphics.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
        }
    }

    private int HitTest(Point clientPoint)
    {
        if (_image is null)
        {
            return -1;
        }

        var layout = GetImageLayout(_image.Size);
        for (var index = _boxes.Count - 1; index >= 0; index--)
        {
            var rect = MapImageRectToClient(_boxes[index].Box, layout, _image.Size);
            if (rect.Contains(clientPoint))
            {
                return index;
            }
        }

        return -1;
    }

    private bool TryMapClientToImage(Point clientPoint, out PointF imagePoint)
    {
        imagePoint = PointF.Empty;
        if (_image is null)
        {
            return false;
        }

        var layout = GetImageLayout(_image.Size);
        if (!layout.Contains(clientPoint))
        {
            return false;
        }

        var scaleX = _image.Width / layout.Width;
        var scaleY = _image.Height / layout.Height;
        imagePoint = new PointF((clientPoint.X - layout.X) * scaleX, (clientPoint.Y - layout.Y) * scaleY);
        return true;
    }

    private RectangleF GetImageLayout(Size imageSize)
    {
        var client = ClientRectangle;
        if (imageSize.Width <= 0 || imageSize.Height <= 0 || client.Width <= 0 || client.Height <= 0)
        {
            return RectangleF.Empty;
        }

        var scale = Math.Min(client.Width / (float)imageSize.Width, client.Height / (float)imageSize.Height);
        var width = imageSize.Width * scale;
        var height = imageSize.Height * scale;
        var x = (client.Width - width) / 2f;
        var y = (client.Height - height) / 2f;
        return new RectangleF(x, y, width, height);
    }

    private static RectangleF MapImageRectToClient(DetectionBox box, RectangleF layout, Size imageSize)
    {
        var scaleX = layout.Width / imageSize.Width;
        var scaleY = layout.Height / imageSize.Height;
        return new RectangleF(
            layout.X + (box.X * scaleX),
            layout.Y + (box.Y * scaleY),
            box.Width * scaleX,
            box.Height * scaleY);
    }
}