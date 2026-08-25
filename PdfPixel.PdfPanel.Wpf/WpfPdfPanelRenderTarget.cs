using PdfPixel.Geometry;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Requests;
using PdfPixel.PdfPanel.Wpf.Drawing;
using SkiaSharp;
using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

partial class WpfPdfPanelRenderTarget : IPdfPanelRenderTarget
{
    public WpfPdfPanelRenderTarget(WriteableBitmap writeableBitmap, WpfPdfPanel panel, PdfSize canvasSize, PdfPoint canvasScale, PdfPoint canvasOffset)
    {
        WriteableBitmap = writeableBitmap ?? throw new ArgumentNullException(nameof(writeableBitmap));
        Panel = panel ?? throw new ArgumentNullException(nameof(panel));
        CanvasSize = canvasSize;
        CanvasScale = canvasScale;
        CanvasOffset = canvasOffset;
    }

    public WriteableBitmap WriteableBitmap { get; }

    public WpfPdfPanel Panel { get; }

    public PdfSize CanvasSize { get; }

    public PdfPoint CanvasScale { get; }

    public PdfPoint CanvasOffset { get; }

    public void Render(SKSurface surface, DrawingRequest request)
    {
        DrawOnWritableBitmap(surface, request);
    }

    private void DrawOnWritableBitmap(SKSurface surface, DrawingRequest request)
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
            Panel.PanelInterface.OnAfterDraw(drawSurface.Canvas, request);
        }

        WriteableBitmap.AddDirtyRect(new Int32Rect(0, 0, imageInfo.Width, imageInfo.Height));

        var drawingVisual = Panel.DrawingVisual;
        DrawingContext render = drawingVisual.RenderOpen();

        var pixelOffsetX = Panel.SnapPosition(CanvasOffset.X, CanvasScale.X);
        var pixelOffsetY = Panel.SnapPosition(CanvasOffset.Y, CanvasScale.Y);

        render.DrawImage(WriteableBitmap, new Rect(pixelOffsetX, pixelOffsetY, WriteableBitmap.Width, WriteableBitmap.Height));

        render.Close();

        WriteableBitmap.Unlock();
    }
}