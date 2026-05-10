using PdfPixel.Models;
using SkiaSharp;
using System;
using System.Threading;

namespace PdfPixel.Commands;

/// <summary>
/// Execution-time context passed to every command during replay.
/// Groups rendering parameters and cancellation into a single object
/// so the <see cref="IPdfCommand.Execute"/> signature stays stable as
/// new per-replay concerns are added.
/// </summary>
public sealed class PdfCommandExecutionContext
{
    public PdfCommandExecutionContext(PdfRenderingParameters renderingParameters, CancellationToken cancellationToken)
    {
        RenderingParameters = renderingParameters ?? throw new ArgumentNullException(nameof(renderingParameters));
        CancellationToken = cancellationToken;
    }

    /// <summary>
    /// Rendering parameters that may vary between replays (e.g. scale factor, antialias).
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; }

    /// <summary>
    /// Cancellation token for cooperative cancellation of command execution.
    /// </summary>
    public CancellationToken CancellationToken { get; }
}
