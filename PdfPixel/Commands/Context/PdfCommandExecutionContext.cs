using PdfPixel.Color;
using PdfPixel.Commands.Cache;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.OptionalContent.Model;
using PdfPixel.TextExtraction;
using System;
using System.Collections.Generic;

namespace PdfPixel.Commands.Context;

/// <summary>
/// Execution-time context passed to every command during replay.
/// Groups rendering parameters and cancellation into a single object so the shape stays
/// stable as new per-replay concerns are added.
/// </summary>
public sealed class PdfCommandExecutionContext : IDisposable
{
    /// <summary>
    /// Initializes a new execution context with the document being replayed, execution parameters,
    /// a content locker, and an observer.
    /// </summary>
    public PdfCommandExecutionContext(
        IPdfDocument document,
        PdfCommandExecutionParameters parameters,
        object contentLocker,
        IReadOnlyDictionary<PdfReference, bool>? optionalContentStates,
        IPdfExecutionObserver executionObserver,
        PdfRectangle? pageRegionOfInterest = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ContentLocker = contentLocker ?? throw new ArgumentNullException(nameof(contentLocker));

        if (optionalContentStates == null)
        {
            PdfOptionalContentProperties? optionalContentProperties = document.OptionalContentProperties;
            if (optionalContentProperties == null)
            {
                optionalContentStates = new Dictionary<PdfReference, bool>();
            }
            else
            {
                optionalContentStates = optionalContentProperties.DefaultConfiguration.ToUserConfiguration().ToStates();
            }
        }

        OptionalContentStates = optionalContentStates;
        MarkedContent = new PdfMarkedContentState(optionalContentStates);
        ExecutionObserver = executionObserver;
        PageRegionOfInterest = pageRegionOfInterest;
    }

    /// <summary>
    /// Color uncolored tiling patterns and uncolored Type 3 glyphs adopt when replayed, or null when
    /// content is drawn with its own colors.
    /// </summary>
    public PdfColor? TintColor { get; internal set; }

    /// <summary>
    /// Document whose commands are replayed, owning the values cached for its whole lifetime.
    /// </summary>
    public IPdfDocument Document { get; }

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
    /// Visible region of the page in page coordinates. Null means the full page is visible.
    /// Used to skip decoding of image tiles outside the visible area.
    /// </summary>
    public PdfRectangle? PageRegionOfInterest { get; }

    /// <summary>
    /// Tracks the total transformation matrix and current clip derived purely from processed commands,
    /// mirroring the save/restore stack independently of how commands are actually drawn.
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
    /// ON/OFF state of the optional content groups content visibility is evaluated under, keyed by group reference.
    /// </summary>
    public IReadOnlyDictionary<PdfReference, bool> OptionalContentStates { get; }

    /// <summary>
    /// Returns the root of the text block tree built during command execution.
    /// Characters are grouped into blocks corresponding to marked content scopes.
    /// </summary>
    public PdfTextBlock GetRootTextBlock() => MarkedContent.GetRootTextBlock();

    /// <inheritdoc />
    public void Dispose() => Cache.Dispose();
}
