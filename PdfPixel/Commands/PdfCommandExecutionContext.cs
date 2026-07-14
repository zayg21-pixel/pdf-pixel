using PdfPixel.Commands.Cache;
using PdfPixel.Models;
using PdfPixel.TextExtraction;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands;

/// <summary>
/// Execution-time context passed to every command during replay.
/// Groups rendering parameters and cancellation into a single object
/// so the <see cref="IPdfCommand.Execute"/> signature stays stable as
/// new per-replay concerns are added.
/// </summary>
public sealed class PdfCommandExecutionContext : IDisposable
{
    /// <summary>
    /// Initializes a new execution context with execution parameters, a content locker, an observer, and the canvas to draw on.
    /// </summary>
    public PdfCommandExecutionContext(
        PdfCommandExecutionParameters parameters,
        object contentLocker,
        IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> optionalContentGroups,
        IPdfExecutionObserver executionObserver,
        SKCanvas canvas,
        SKRect? pageRegionOfInterest = null)
    {
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ContentLocker = contentLocker ?? throw new ArgumentNullException(nameof(contentLocker));
        OptionalContentGroups = optionalContentGroups ?? throw new ArgumentNullException(nameof(optionalContentGroups));
        MarkedContent = new PdfMarkedContentState(OptionalContentGroups);
        ExecutionObserver = executionObserver;
        Canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));
        PageRegionOfInterest = pageRegionOfInterest;
    }

    /// <summary>
    /// Current paint uncolored modifier.
    /// </summary>
    public UncoloredPaintModifier? UncoloredModifier { get; internal set; }

    /// <summary>
    /// Execution parameters that may vary between replays (e.g. scale factor, antialias).
    /// </summary>
    public PdfCommandExecutionParameters Parameters { get; }

    /// <summary>
    /// Locker to prevent multi-threaded access to PDF content stream and lazy-initialized data.
    /// </summary>
    public object ContentLocker { get; }

    /// <summary>
    /// Cancellation token for cooperative cancellation of command execution.
    /// </summary>
    public IPdfExecutionObserver ExecutionObserver { get; }

    /// <summary>
    /// The canvas commands draw onto during this replay.
    /// </summary>
    public SKCanvas Canvas { get; private set; }

    /// <summary>
    /// Visible region of the page in page coordinates. Null means the full page is visible.
    /// Used to skip decoding of image tiles outside the visible area.
    /// </summary>
    public SKRect? PageRegionOfInterest { get; }

    /// <summary>
    /// True when at least one command signalled that the rendered output covers only a partial
    /// region of the page content (e.g. an image whose tiles were only partially decoded due to
    /// the viewport ROI). The cached picture must be regenerated whenever the viewport changes.
    /// </summary>
    public bool IsPartialContent { get; private set; }

    /// <summary>
    /// Marks the current execution as producing partial content. Commands call this when they
    /// detect that their output is clipped to the visible region rather than the full image.
    /// </summary>
    public void SetPartialContent() => IsPartialContent = true;

    /// <summary>
    /// The command currently being executed by the active <see cref="IPdfCommandProcessor"/>.
    /// Set by the processor immediately after <see cref="IPdfCommand.Execute"/> returns and before
    /// <see cref="IPdfExecutionObserver.Notify"/> is called, so observers always see the command
    /// at the current nesting level rather than the last sub-command of a nested replay.
    /// </summary>
    public IPdfCommand? CurrentCommand { get; set; }

    /// <summary>
    /// Tracks the total transformation matrix and clip path derived purely from processed commands,
    /// mirroring the canvas save/restore stack without depending on the canvas itself.
    /// </summary>
    public PdfCommandExecutionFrames Frames { get; } = new();

    /// <summary>
    /// General-purpose cache for values commands want to reuse across this execution's replay.
    /// </summary>
    public CommandCache Cache { get; } = new();

    /// <summary>
    /// Tracks active marked content scopes and evaluates optional content visibility.
    /// </summary>
    public PdfMarkedContentState MarkedContent { get; }

    /// <summary>
    /// Optional content groups (layers) defined by the document, keyed by their indirect reference.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, PdfOptionalContentGroup> OptionalContentGroups { get; }

    /// <summary>
    /// Root of the text block tree built during command execution.
    /// Characters are grouped into blocks corresponding to marked content scopes.
    /// </summary>
    public PdfTextBlock RootTextBlock => MarkedContent.RootTextBlock;

    /// <summary>
    /// Disposes the current canvas, replaces it with <paramref name="canvas"/>, and replays the
    /// accumulated frame state onto the new canvas so it is in the same transformation and clip state.
    /// </summary>
    public void UpdateCanvas(SKCanvas canvas)
    {
        if (canvas == null)
        {
            throw new ArgumentNullException(nameof(canvas));
        }

        Canvas.Dispose();
        Canvas = canvas;
        Frames.ApplyStateTo(canvas);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Frames.Dispose();
        Cache.Dispose();
        Canvas.Dispose();
    }
}
