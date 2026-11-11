using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Wpf.Drawing;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace PdfPixel.PdfPanel.Wpf;

internal sealed partial class WpfPdfPanelRenderTarget : IPdfPanelRenderTarget
{
    private const int BitsPerComponent = 8;

    // WPF's Pbgra32 stores blue, green, red, then premultiplied alpha.
    private static readonly PdfImageChannel[] BgraChannels = [PdfImageChannel.Component(2), PdfImageChannel.Component(1), PdfImageChannel.Component(0), PdfImageChannel.Alpha];

    private PdfImageSamples? _samples;

    public WpfPdfPanelRenderTarget(WriteableBitmap writeableBitmap, WpfPdfPanel panel, in PdfSize panelSize, in PdfPoint hostScale, in PdfPoint hostOffset)
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
    public PdfImageSamples GetFrameSamples(int width, int height)
    {
        if (_samples == null || _samples.Width != width || _samples.Height != height)
        {
            _samples = new PdfImageSamples(width, height, PdfImageSampleFormat.Integer, BitsPerComponent, BgraChannels, isPremultiplied: true);
        }

        return _samples;
    }

    /// <inheritdoc />
    public void Present(PdfPanelFrame frame)
    {
        if (Panel.DrawingVisual == null || _samples == null)
        {
            return;
        }

        int width = WriteableBitmap.PixelWidth;
        int height = WriteableBitmap.PixelHeight;

        if (width != _samples.Width || height != _samples.Height)
        {
            return;
        }

        GCHandle pixels = _samples.PinBuffer();

        try
        {
            WriteableBitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels.AddrOfPinnedObject(), _samples.RowBytes * height, _samples.RowBytes);
        }
        finally
        {
            pixels.Free();
        }

        WriteableBitmap.Lock();

        DrawingVisual drawingVisual = Panel.DrawingVisual;
        DrawingContext render = drawingVisual.RenderOpen();

        double pixelOffsetX = Panel.SnapPosition(HostOffset.X, HostScale.X);
        double pixelOffsetY = Panel.SnapPosition(HostOffset.Y, HostScale.Y);

        render.DrawImage(WriteableBitmap, new Rect(pixelOffsetX, pixelOffsetY, WriteableBitmap.Width, WriteableBitmap.Height));

        render.Close();

        WriteableBitmap.Unlock();
    }
}
