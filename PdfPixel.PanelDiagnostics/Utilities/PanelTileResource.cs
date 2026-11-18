using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.PdfPanel.Rendering;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// A tile resource of a <see cref="PanelTileLog"/> that keeps no pixels and reports its writes and its disposal to
/// the log.
/// </summary>
internal sealed class PanelTileResource : IPdfPanelTileResource
{
    private readonly PanelTileLog _log;

    public PanelTileResource(PanelTileLog log) => _log = log;

    /// <inheritdoc />
    public void Write(PdfImageSamples samples) => _log.OnTileWritten();

    /// <inheritdoc />
    public void Dispose() => _log.OnTileDisposed();
}
