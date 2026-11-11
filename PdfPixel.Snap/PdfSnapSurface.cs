using Microsoft.Extensions.Logging;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Commands.Execution;
using PdfPixel.Content;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Text;
using PdfPixel.Snap.Transparency;
using PdfPixel.Tagging;
using PdfPixel.Transparency;
using System;
using System.Collections.Generic;

namespace PdfPixel.Snap;

/// <summary>
/// A CPU rasterizer drawing recorded PDF commands onto a transparent surface of <see cref="Width"/> by
/// <see cref="Height"/> pixels; other regions of a page are moved onto it with a matrix.
/// </summary>
public sealed partial class PdfSnapSurface : IPdfCommandProcessor, IDisposable
{
    internal const int MaximumComponentCount = 4;

    // Pixels by which bounds are widened before skipping what lies outside the draw limits.
    private const float DrawLimitsMargin = 2;

    // Index of the gray function among a transfer function's four.
    private const int GrayTransferFunctionIndex = 3; // TODO: [HIGH] duplicated shall be on transfer function itself

    // The paint a group is composited with when the PDF gives it none: Normal, opaque, the default color conversion.
    private static readonly PdfPaint DefaultGroupPaint = new(PdfPaintStyle.Fill);

    // The group of a text object under text knockout (ISO 32000-2 9.3.8).
    private static readonly PdfTransparencyGroupContext TextObjectGroup = new(null, isolated: false, knockout: true);

    private readonly ILogger<PdfSnapSurface> _logger;
    private readonly Stack<SnapGraphicsState> _savedStates = [];
    private readonly HashSet<PdfCommandKind> _reportedUnsupportedKinds = []; // TODO: remove
    private readonly SnapLayer _root;
    private readonly SnapMemoryBlock? _rootBlock;
    private readonly IPdfImageCache _imageCache;
    private readonly float[] _sourceColor; // TODO: probably better use stack alloc
    private readonly float[] _groupColor;
    private PdfCommandExecutionContext _executionContext;
    private SnapGraphicsState _state;
    private SnapTextObject? _textObject;

    /// <summary>
    /// Initializes a surface of <paramref name="width"/> by <paramref name="height"/> pixels created with
    /// <paramref name="parameters"/>, drawing with <paramref name="executionContext"/>.
    /// </summary>
    /// <param name="width">Width in pixels; zero for a surface that draws nothing.</param>
    /// <param name="height">Height in pixels; zero for a surface that draws nothing.</param>
    /// <param name="parameters">What the surface keeps for its lifetime: its root color space and memory pool.</param>
    /// <param name="executionContext">The execution context commands are drawn with.</param>
    /// <param name="logger">The logger failed and unsupported commands are reported to.</param>
    public PdfSnapSurface(int width, int height, PdfSnapSurfaceParameters parameters, PdfCommandExecutionContext executionContext, ILogger<PdfSnapSurface> logger)
        : this(width, height, RentRootBlock(width, height, parameters), parameters, executionContext, logger)
    {
    }

    /// <summary>
    /// Initializes a surface drawing onto <paramref name="samples"/>, created with <paramref name="parameters"/>,
    /// drawing with <paramref name="executionContext"/>; what the samples hold is kept as already painted.
    /// </summary>
    /// <param name="samples">
    /// The samples drawn onto, the size of the surface, owned by the caller; zero-sized for a surface that draws nothing.
    /// </param>
    /// <param name="parameters">What the surface keeps for its lifetime: its root color space and memory pool.</param>
    /// <param name="executionContext">The execution context commands are drawn with.</param>
    /// <param name="logger">The logger failed and unsupported commands are reported to.</param>
    public PdfSnapSurface(PdfImageSamples samples, PdfSnapSurfaceParameters parameters, PdfCommandExecutionContext executionContext, ILogger<PdfSnapSurface> logger)
        : this(samples ?? throw new ArgumentNullException(nameof(samples)), rootBlock: null, parameters, executionContext, logger)
    {
        _root.MarkAllPainted();
    }

    private PdfSnapSurface(int width, int height, SnapMemoryBlock rootBlock, PdfSnapSurfaceParameters parameters, PdfCommandExecutionContext executionContext, ILogger<PdfSnapSurface> logger)
        : this(CreateRootResult(width, height, rootBlock, parameters), rootBlock, parameters, executionContext, logger)
    {
    }

    private PdfSnapSurface(PdfImageSamples rootSamples, SnapMemoryBlock? rootBlock, PdfSnapSurfaceParameters parameters, PdfCommandExecutionContext executionContext, ILogger<PdfSnapSurface> logger)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        Width = rootSamples.Width;
        Height = rootSamples.Height;
        Scratch = new SnapScratchBuffer(parameters.MemoryPool);
        _imageCache = parameters.ImageCache;
        _executionContext = executionContext ?? throw new ArgumentNullException(nameof(executionContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        PdfIntegerRectangle rootBounds = new(0, 0, Width, Height);
        SnapLayerGroup resolvedRootGroup = SnapLayerGroup.Resolve(CreateRootGroup(parameters.RootColorSpace), parent: null);
        _rootBlock = rootBlock;
        _root = new SnapLayer(rootSamples, rootBounds, resolvedRootGroup, parent: null, Scratch, GetConverters(), keepsShape: false);
        _state = new SnapGraphicsState(_root, executionContext.VisibleRegion);
        _sourceColor = new float[MaximumComponentCount];
        _groupColor = new float[MaximumComponentCount];
    }

    /// <summary>
    /// Gets the width of the surface in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Gets the height of the surface in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Gets whether the surface has no pixels: it draws nothing and only follows the content of the commands.
    /// </summary>
    private bool IsEmpty => Width == 0 || Height == 0;

    internal SnapScratchBuffer Scratch { get; }

    private SnapColorConverterCache GetConverters() => SnapColorConverterCache.Get(_executionContext.Cache, _executionContext.Parameters.DeviceProfiles);

    /// <summary>
    /// Returns whether the content being drawn belongs to the description of a Type 3 glyph: some open scope is the
    /// content stream of a glyph.
    /// </summary>
    private bool IsType3GlyphContent()
    {
        foreach (PdfScope scope in _executionContext.ContentScopes.Scopes)
        {
            if (scope is PdfContentStreamScope contentStreamScope && contentStreamScope.StreamType == PdfContentStreamType.Type3Glyph)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Draws commands with <paramref name="executionContext"/> from now on, keeping what was painted.
    /// </summary>
    /// <param name="executionContext">The execution context commands are drawn with.</param>
    public void UpdateContext(PdfCommandExecutionContext executionContext)
    {
        if (executionContext == null)
        {
            throw new ArgumentNullException(nameof(executionContext));
        }

        if (!ReferenceEquals(_state.Target, _root))
        {
            throw new InvalidOperationException("The execution context can only be updated outside groups.");
        }

        _executionContext = executionContext;
        _state.VisibleRegion = executionContext.VisibleRegion;
        _root.UpdateConverters(GetConverters());
    }

    /// <summary>
    /// Clears all drawing state; the caller restarts the scopes and the content tree with
    /// <see cref="PdfContentScopeState.Reset"/> and <see cref="PdfContentTreeBuilder.Reset"/>.
    /// </summary>
    public void Reset()
    {
        DisposeStates();
        _savedStates.Clear();
        _state = new SnapGraphicsState(_root, _executionContext.VisibleRegion);
        _textObject?.Dispose();
        _textObject = null;
    }

    /// <summary>
    /// Saves the current matrix, clip and drawing state, restored by the matching <see cref="Restore"/>.
    /// </summary>
    public void Save()
    {
        _savedStates.Push(_state);
        _state = _state.Clone();
    }

    /// <summary>
    /// Restores the state of the last <see cref="Save"/>; does nothing when there is none.
    /// </summary>
    public void Restore()
    {
        if (_savedStates.Count == 0)
        {
            return;
        }

        _state.Dispose();
        _state = _savedStates.Pop();

        if (_textObject?.Group != null && ReferenceEquals(_state.Target, _textObject.Parent))
        {
            _state.Target = _textObject.Group;
        }
    }

    /// <summary>
    /// Concatenates <paramref name="matrix"/> onto the current matrix.
    /// </summary>
    /// <param name="matrix">The matrix applied before the current one.</param>
    public void Concat(in PdfMatrix matrix) => _state.DeviceMatrix = _state.DeviceMatrix.PreConcat(matrix);

    /// <summary>
    /// Returns the isolated, non-knockout group the root layer of a surface composites in.
    /// </summary>
    private static PdfTransparencyGroupContext CreateRootGroup(PdfColorSpaceConverter colorSpace)
    {
        if (colorSpace == null)
        {
            throw new ArgumentNullException(nameof(colorSpace));
        }

        return new PdfTransparencyGroupContext(colorSpace, isolated: true, knockout: false);
    }

    /// <summary>
    /// Rents the block of the transparent root a surface of <paramref name="width"/> by <paramref name="height"/>
    /// pixels owns.
    /// </summary>
    private static SnapMemoryBlock RentRootBlock(int width, int height, PdfSnapSurfaceParameters parameters)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (width < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must not be negative.");
        }

        if (height < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must not be negative.");
        }

        SnapLayerGroup resolvedRootGroup = SnapLayerGroup.Resolve(CreateRootGroup(parameters.RootColorSpace), parent: null);

        return parameters.MemoryPool.Rent(SnapLayer.GetResultLength(new PdfIntegerRectangle(0, 0, width, height), resolvedRootGroup));
    }

    /// <summary>
    /// Creates the transparent root of a surface of <paramref name="width"/> by <paramref name="height"/> pixels in
    /// <paramref name="rootBlock"/>.
    /// </summary>
    private static PdfImageSamples CreateRootResult(int width, int height, SnapMemoryBlock rootBlock, PdfSnapSurfaceParameters parameters)
    {
        SnapLayerGroup resolvedRootGroup = SnapLayerGroup.Resolve(CreateRootGroup(parameters.RootColorSpace), parent: null);

        return SnapLayer.CreateResult(new PdfIntegerRectangle(0, 0, width, height), resolvedRootGroup, rootBlock.Array);
    }

    /// <summary>
    /// Disposes the current and the saved states.
    /// </summary>
    private void DisposeStates()
    {
        _state.Dispose();

        foreach (SnapGraphicsState savedState in _savedStates)
        {
            savedState.Dispose();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        DisposeStates();
        _textObject?.Dispose();
        _root.Dispose();
        _rootBlock?.Dispose();
    }
}
