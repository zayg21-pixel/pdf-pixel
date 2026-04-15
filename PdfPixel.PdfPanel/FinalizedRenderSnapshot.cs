using PdfPixel.PdfPanel.Requests;
using SkiaSharp;
using System;

namespace PdfPixel.PdfPanel;

/// <summary>
/// Holds a snapshot of the fully rendered surface and the corresponding drawing request.
/// Created by the <see cref="PdfPanelRenderCommandType.Finalize"/> command after a complete
/// render pack succeeds. Consumed and disposed by the next <see cref="PdfPanelRenderCommandType.DrawBackground"/> command.
/// </summary>
internal sealed class FinalizedRenderSnapshot : IDisposable
{
    /// <summary>
    /// Initializes a new instance of <see cref="FinalizedRenderSnapshot"/>.
    /// </summary>
    /// <param name="surfaceSnapshot">The snapshot image captured from the fully rendered surface.</param>
    /// <param name="request">The drawing request that produced this snapshot.</param>
    public FinalizedRenderSnapshot(SKImage surfaceSnapshot, PagesDrawingRequest request)
    {
        SurfaceSnapshot = surfaceSnapshot ?? throw new ArgumentNullException(nameof(surfaceSnapshot));
        Request = request ?? throw new ArgumentNullException(nameof(request));
    }

    /// <summary>
    /// The snapshot image of the fully rendered surface.
    /// </summary>
    public SKImage SurfaceSnapshot { get; }

    /// <summary>
    /// The drawing request that was active when this snapshot was taken.
    /// </summary>
    public PagesDrawingRequest Request { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        SurfaceSnapshot.Dispose();
    }
}
