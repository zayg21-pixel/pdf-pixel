using Microsoft.Extensions.Logging;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Rendering;
using PdfPixel.PdfPanel.Web.Emscripten;
using System;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace PdfPixel.PdfPanel.Web.Rendering;

/// <summary>
/// Presents panel frames on a browser canvas: the frame is drawn onto RGBA samples in the WebAssembly heap and put on
/// the canvas's 2D context through Emscripten.
/// </summary>
[SupportedOSPlatform("browser")]
internal sealed class CanvasRenderTarget : IPdfPanelRenderTargetFactory, IPdfPanelRenderTarget
{
    private const int BitsPerComponent = 8;

    // Frames are opaque, so their premultiplied RGBA is the unpremultiplied RGBA the canvas expects.
    private static readonly PdfImageChannel[] RgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

    private readonly string _canvasSelector;
    private readonly ILogger _logger;
    private readonly Action<PdfPanelFrame> _onFramePresented;
    private PdfImageSamples? _samples;

    public CanvasRenderTarget(ILogger logger, string canvasSelector, Action<PdfPanelFrame> onFramePresented)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _canvasSelector = canvasSelector ?? throw new ArgumentNullException(nameof(canvasSelector));
        _onFramePresented = onFramePresented ?? throw new ArgumentNullException(nameof(onFramePresented));
    }

    /// <inheritdoc />
    public PdfMatrix HostToPanel => PdfMatrix.Identity;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public PdfImageSamples GetFrameSamples(int width, int height)
    {
        if (_samples == null || _samples.Width != width || _samples.Height != height)
        {
            EmscriptenInterop.SetCanvasSize(_canvasSelector, width, height);
            _samples = new PdfImageSamples(width, height, PdfImageSampleFormat.Integer, BitsPerComponent, RgbaChannels, isPremultiplied: true);
        }

        return _samples;
    }

    /// <inheritdoc />
    public void Present(PdfPanelFrame frame)
    {
        if (_samples == null)
        {
            return;
        }

        GCHandle pixels = _samples.PinBuffer();

        try
        {
            EmscriptenInterop.SetCanvasRgba(_canvasSelector, pixels.AddrOfPinnedObject(), _samples.Width, _samples.Height);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to present the frame on canvas {CanvasSelector}", _canvasSelector);
            return;
        }
#pragma warning restore CA1031
        finally
        {
            pixels.Free();
        }

        _onFramePresented(frame);
    }
}
