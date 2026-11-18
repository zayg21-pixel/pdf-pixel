using PdfPixel.PdfPanel.Thumbnails;

namespace PdfPixel.PanelDiagnostics.Utilities;

/// <summary>
/// A thumbnail resource that keeps no pixels.
/// </summary>
internal sealed class PanelThumbnailResource : IPdfPanelThumbnailResource
{
    /// <inheritdoc />
    public void Dispose()
    {
    }
}
