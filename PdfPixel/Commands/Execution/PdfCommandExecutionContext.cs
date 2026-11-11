using PdfPixel.Annotations;
using PdfPixel.Color;
using PdfPixel.Commands.Cache;
using PdfPixel.Content;
using PdfPixel.Geometry;
using PdfPixel.OptionalContent;
using System;

namespace PdfPixel.Commands.Execution;

/// <summary>
/// Execution-time context passed to every command during replay.
/// Groups rendering parameters and cancellation into a single object so the shape stays
/// stable as new per-replay concerns are added.
/// </summary>
public sealed class PdfCommandExecutionContext
{
    /// <summary>
    /// Initializes a new execution context with the cache commands reuse values from, execution parameters,
    /// a content locker, the optional content states, the annotation states and an observer.
    /// </summary>
    public PdfCommandExecutionContext(
        CommandCache cache,
        PdfCommandExecutionParameters parameters,
        object contentLocker,
        PdfOptionalContentStates optionalContentStates,
        PdfAnnotationStates annotationStates,
        IPdfExecutionObserver executionObserver,
        PdfRectangle? visibleRegion = null)
    {
        Cache = cache ?? throw new ArgumentNullException(nameof(cache));
        Parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        ContentLocker = contentLocker ?? throw new ArgumentNullException(nameof(contentLocker));
        OptionalContentStates = optionalContentStates ?? throw new ArgumentNullException(nameof(optionalContentStates));
        AnnotationStates = annotationStates ?? throw new ArgumentNullException(nameof(annotationStates));
        ContentScopes = new PdfContentScopeState(optionalContentStates);
        ContentTree = new PdfContentTreeBuilder();
        ExecutionObserver = executionObserver;
        VisibleRegion = visibleRegion;
    }

    /// <summary>
    /// General-purpose cache for values commands want to reuse, owned by the caller.
    /// </summary>
    public CommandCache Cache { get; }

    /// <summary>
    /// Execution parameters that may vary between replays (e.g. scale factor, antialias).
    /// </summary>
    public PdfCommandExecutionParameters Parameters { get; }

    /// <summary>
    /// Locker to prevent multi-threaded access to PDF content streams.
    /// </summary>
    public object ContentLocker { get; }

    /// <summary>
    /// Cancellation token for cooperative cancellation of command execution.
    /// </summary>
    public IPdfExecutionObserver ExecutionObserver { get; }

    /// <summary>
    /// Part of the page visible across the replays that share decoded image tiles, in the device space of the surface
    /// the commands are replayed on; it may reach beyond that surface. Null means the full page.
    /// </summary>
    public PdfRectangle? VisibleRegion { get; }

    /// <summary>
    /// Tracks active scopes and evaluates optional content visibility.
    /// </summary>
    public PdfContentScopeState ContentScopes { get; }

    /// <summary>
    /// Builds the content tree.
    /// </summary>
    public PdfContentTreeBuilder ContentTree { get; }

    /// <summary>
    /// ON/OFF state of the optional content groups content visibility is evaluated under, keyed by group reference.
    /// </summary>
    public PdfOptionalContentStates OptionalContentStates { get; }

    /// <summary>
    /// States the annotations of recorded <see cref="DrawAnnotationCommand"/>s are drawn in.
    /// </summary>
    public PdfAnnotationStates AnnotationStates { get; }
}
