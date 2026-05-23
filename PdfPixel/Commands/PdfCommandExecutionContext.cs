using PdfPixel.Models;
using SkiaSharp;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// Execution-time context passed to every command during replay.
/// Groups rendering parameters and cancellation into a single object
/// so the <see cref="IPdfCommand.Execute"/> signature stays stable as
/// new per-replay concerns are added.
/// </summary>
public sealed class PdfCommandExecutionContext
{
    public PdfCommandExecutionContext(PdfRenderingParameters renderingParameters, object contentLocker, IPdfExecutionObserver executionObserver, SKRect? pageRegionOfInterest = null)
    {
        RenderingParameters = renderingParameters ?? throw new ArgumentNullException(nameof(renderingParameters));
        ContentLocker = contentLocker ?? throw new ArgumentNullException(nameof(contentLocker));
        ExecutionObserver = executionObserver;
        PageRegionOfInterest = pageRegionOfInterest;
    }

    /// <summary>
    /// Rendering parameters that may vary between replays (e.g. scale factor, antialias).
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; }

    /// <summary>
    /// Locker to prevent multi-threaded access to PDF content stream and lazy-initialized data.
    /// </summary>
    public object ContentLocker { get; }

    /// <summary>
    /// Cancellation token for cooperative cancellation of command execution.
    /// </summary>
    public IPdfExecutionObserver ExecutionObserver { get; }

    /// <summary>
    /// Visible region of the page in page coordinates. Null means the full page is visible.
    /// Used to skip decoding of image tiles outside the visible area.
    /// </summary>
    public SKRect? PageRegionOfInterest { get; }
}
