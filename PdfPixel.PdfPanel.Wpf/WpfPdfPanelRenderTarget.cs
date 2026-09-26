using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

partial class WpfPdfPanelRenderTarget : IPdfPanelRenderTarget
{
    public WpfPdfPanelRenderTarget(WriteableBitmap writeableBitmap, WpfPdfPanel panel, PdfSize panelSize, PdfPoint hostScale, PdfPoint hostOffset)
    {
        WriteableBitmap = writeableBitmap ?? throw new ArgumentNullException(nameof(writeableBitmap));
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        PanelSize = panelSize;
        HostScale = hostScale;
        HostOffset = hostOffset;
        HostToPanel = panel.GetHostToPanelMatrix(hostOffset, hostScale);
    }

    public WriteableBitmap WriteableBitmap { get; }

    public WpfPdfPanel Panel { get; }

    public PdfSize PanelSize { get; }

    public PdfPoint HostScale { get; }

    public PdfPoint HostOffset { get; }

    /// <inheritdoc />
    public PdfMatrix HostToPanel { get; }

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame)
    {
        DrawOnWritableBitmap(surface, frame);
    }

    private void DrawOnWritableBitmap(SKSurface surface, PdfPanelFrame frame)
    {
        if (WriteableBitmap.PixelWidth != surface.Canvas.DeviceClipBounds.Width || WriteableBitmap.PixelHeight != surface.Canvas.DeviceClipBounds.Height)
        {
            return;
        }

        WriteableBitmap.Lock();

        SKImageInfo imageInfo = new SKImageInfo(WriteableBitmap.PixelWidth, WriteableBitmap.PixelHeight, SKColorType.Bgra8888, SKAlphaType.Premul);

        surface.ReadPixels(imageInfo, WriteableBitmap.BackBuffer, WriteableBitmap.BackBufferStride, 0, 0);

        if (Panel.PanelInterface.OnAfterDraw != null)
        {
            using SKSurface drawSurface = SKSurface.Create(imageInfo, WriteableBitmap.BackBuffer, WriteableBitmap.BackBufferStride);
            Panel.PanelInterface.OnAfterDraw(drawSurface.Canvas, frame);
        }

        WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));

        var drawingVisual = Panel.DrawingVisual;
        DrawingContext render = drawingVisual.RenderOpen();

        var pixelOffsetX = Panel.SnapPosition(HostOffset.X, HostScale.X);
        var pixelOffsetY = Panel.SnapPosition(HostOffset.Y, HostScale.Y);

        render.DrawImage(WriteableBitmap, new Rect(pixelOffsetX, pixelOffsetY, WriteableBitmap.Width, WriteableBitmap.Height));

        render.Close();

        WriteableBitmap.Unlock();
    }
}
