using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Rendering;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// Render target that logs every presented frame.
/// </summary>
internal sealed class PanelRenderTarget : IPdfPanelRenderTarget, IPdfPanelRenderTargetFactory
{
    private const int BitsPerComponent = 8;

    // The layout WPF presents: blue, green, red, then premultiplied alpha.
    private static readonly PdfImageChannel[] BgraChannels = [PdfImageChannel.Component(2), PdfImageChannel.Component(1), PdfImageChannel.Component(0), PdfImageChannel.Alpha];

    private readonly Stopwatch _clock;
    private PdfImageSamples? _samples;

    /// <summary>
    /// Initializes the render target with the clock its log timestamps are read from.
    /// </summary>
    public PanelRenderTarget(Stopwatch clock) => _clock = clock;

    /// <inheritdoc />
    public PdfMatrix HostToPanel => PdfMatrix.Identity;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

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
    public void Present()
    {
        // TODO: log the pages of the frame and the scale and region their content was rendered at, which the render
        // target no longer receives.
        Console.WriteLine($"{_clock.Elapsed.TotalMilliseconds,10:F1} ms  frame {_samples?.Width}x{_samples?.Height}");
    }
}
