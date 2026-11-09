using System;
using System.IO;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;

namespace Bond.Views;

/// <summary>
/// 头像裁切窗口：固定 360×360 视口 + 圆形裁切框，拖动/滚轮缩放后输出 512×512 PNG。
/// <para>只做本地裁切，不把原图上传；裁切结果交给 <c>SettingsPageViewModel.UploadAvatarAsync</c>。</para>
/// </summary>
public partial class AvatarCropWindow : Window
{
    private const double ViewportSize = 360;
    private const int OutputSize = 512;

    private Bitmap? _source;
    private double _baseScale = 1;
    private double _zoom = 1;
    private double _offsetX;
    private double _offsetY;
    private bool _dragging;
    private Point _dragStart;
    private double _dragStartOffsetX;
    private double _dragStartOffsetY;

    public string? CroppedPath { get; private set; }

    public AvatarCropWindow()
    {
        InitializeComponent();
    }

    public AvatarCropWindow(string sourcePath) : this()
    {
        LoadSource(sourcePath);
    }

    private void LoadSource(string sourcePath)
    {
        _source = new Bitmap(sourcePath);
        var pixel = _source.PixelSize;
        if (pixel.Width <= 0 || pixel.Height <= 0)
        {
            Close(false);
            return;
        }

        // 保证图片始终覆盖 360×360 视口，避免裁切框内出现空白。
        _baseScale = Math.Max(ViewportSize / pixel.Width, ViewportSize / pixel.Height);
        PreviewImage.Source = _source;
        ApplyTransform();
    }

    private void ApplyTransform()
    {
        if (_source is null) return;

        var scale = _baseScale * _zoom;
        var width = _source.PixelSize.Width * scale;
        var height = _source.PixelSize.Height * scale;

        var maxOffsetX = Math.Max(0, (width - ViewportSize) / 2);
        var maxOffsetY = Math.Max(0, (height - ViewportSize) / 2);
        _offsetX = Math.Clamp(_offsetX, -maxOffsetX, maxOffsetX);
        _offsetY = Math.Clamp(_offsetY, -maxOffsetY, maxOffsetY);

        PreviewImage.Width = width;
        PreviewImage.Height = height;
        Canvas.SetLeft(PreviewImage, (ViewportSize - width) / 2 + _offsetX);
        Canvas.SetTop(PreviewImage, (ViewportSize - height) / 2 + _offsetY);
        ZoomText.Text = $"{_zoom * 100:0}%";
    }

    private void SetZoom(double value)
    {
        if (_source is null) return;

        var newZoom = Math.Clamp(value, 1, 3);
        if (Math.Abs(newZoom - _zoom) < 0.0001) return;

        // 以视口中心为锚点缩放，避免滑块一动图片就跳走。
        var oldScale = _baseScale * _zoom;
        var oldWidth = _source.PixelSize.Width * oldScale;
        var oldHeight = _source.PixelSize.Height * oldScale;
        var oldLeft = (ViewportSize - oldWidth) / 2 + _offsetX;
        var oldTop = (ViewportSize - oldHeight) / 2 + _offsetY;
        var centerSourceX = (ViewportSize / 2 - oldLeft) / oldScale;
        var centerSourceY = (ViewportSize / 2 - oldTop) / oldScale;

        _zoom = newZoom;
        var newScale = _baseScale * _zoom;
        var newWidth = _source.PixelSize.Width * newScale;
        var newHeight = _source.PixelSize.Height * newScale;
        _offsetX = ViewportSize / 2 - centerSourceX * newScale - (ViewportSize - newWidth) / 2;
        _offsetY = ViewportSize / 2 - centerSourceY * newScale - (ViewportSize - newHeight) / 2;

        ApplyTransform();
    }

    private void OnZoomChanged(object? sender, Avalonia.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        SetZoom(e.NewValue);
    }

    private void OnViewportPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_source is null) return;
        if (!e.GetCurrentPoint(ViewportBorder).Properties.IsLeftButtonPressed) return;

        _dragging = true;
        _dragStart = e.GetPosition(ViewportBorder);
        _dragStartOffsetX = _offsetX;
        _dragStartOffsetY = _offsetY;
        e.Pointer.Capture(ViewportBorder);
        e.Handled = true;
    }

    private void OnViewportPointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_dragging) return;

        var point = e.GetPosition(ViewportBorder);
        _offsetX = _dragStartOffsetX + (point.X - _dragStart.X);
        _offsetY = _dragStartOffsetY + (point.Y - _dragStart.Y);
        ApplyTransform();
        e.Handled = true;
    }

    private void OnViewportPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!_dragging) return;

        _dragging = false;
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    private void OnViewportWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_source is null) return;

        var next = Math.Clamp(_zoom + e.Delta.Y * 0.12, 1, 3);
        ZoomSlider.Value = next;
        SetZoom(next);
        e.Handled = true;
    }

    private void OnConfirmClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_source is null) return;

        var scale = _baseScale * _zoom;
        var width = _source.PixelSize.Width * scale;
        var height = _source.PixelSize.Height * scale;
        var left = (ViewportSize - width) / 2 + _offsetX;
        var top = (ViewportSize - height) / 2 + _offsetY;

        var sourceX = (0 - left) / scale;
        var sourceY = (0 - top) / scale;
        var sourceSize = ViewportSize / scale;

        sourceX = Math.Clamp(sourceX, 0, Math.Max(0, _source.PixelSize.Width - sourceSize));
        sourceY = Math.Clamp(sourceY, 0, Math.Max(0, _source.PixelSize.Height - sourceSize));
        sourceSize = Math.Min(sourceSize, Math.Min(_source.PixelSize.Width, _source.PixelSize.Height));

        var bitmap = new RenderTargetBitmap(new PixelSize(OutputSize, OutputSize), new Vector(96, 96));
        using (var context = bitmap.CreateDrawingContext())
        {
            context.DrawImage(
                _source,
                new Rect(sourceX, sourceY, sourceSize, sourceSize),
                new Rect(0, 0, OutputSize, OutputSize));
        }

        var path = Path.Combine(Path.GetTempPath(), $"bond-avatar-crop-{Guid.NewGuid():N}.png");
        bitmap.Save(path);
        CroppedPath = path;
        Close(true);
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);

    protected override void OnClosed(EventArgs e)
    {
        _source?.Dispose();
        _source = null;
        base.OnClosed(e);
    }
}
