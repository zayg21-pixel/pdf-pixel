using PdfPixel.Geometry;
using PdfPixel.PdfPanel;
using PdfPixel.PdfPanel.Rendering;
using SkiaSharp;
using System.Diagnostics;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// Render target that logs every presented frame with the pages it shows.
/// </summary>
internal sealed class PanelRenderTarget : IPdfPanelRenderTarget, IPdfPanelRenderTargetFactory
{
    private readonly Stopwatch _clock;

    /// <summary>
    /// Initializes the render target with the clock its log timestamps are read from.
    /// </summary>
    public PanelRenderTarget(Stopwatch clock) => _clock = clock;

    /// <inheritdoc />
    public PdfMatrix HostToPanel => PdfMatrix.Identity;

    /// <inheritdoc />
    public IPdfPanelRenderTarget GetRenderTarget(PdfPanelContext context) => this;

    /// <inheritdoc />
    public void Render(SKSurface surface, PdfPanelFrame frame)
    {
        string pages = string.Join(", ", frame.Pages.Select(FormatPage));
        Console.WriteLine($"{_clock.Elapsed.TotalMilliseconds,10:F1} ms  frame, pages {pages}");
    }

    private static string FormatPage(PdfPanelFramePage page)
        => $"{page.PageNumber} [content {FormatLayer(page.RenderedContent)}, annotations {FormatLayer(page.RenderedAnnotations)}]";

    private static string FormatLayer(PdfPanelRenderedLayerInfo? layer)
    {
        if (layer == null)
        {
            return "-";
        }

        string region = (layer.Value.RegionOfInterest == null) ? "full" : $"roi {layer.Value.RegionOfInterest.Value}";

        return $"scale {layer.Value.Scale} {region}";
    }
}
