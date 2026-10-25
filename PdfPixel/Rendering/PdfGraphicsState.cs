using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Fonts;
using PdfPixel.Geometry;
using PdfPixel.Rendering.Text;
using PdfPixel.Transparency;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering;

/// <summary>
/// Graphics state for PDF rendering - corresponds to the PDF graphics state stack (q/Q operators).
/// </summary>
public class PdfGraphicsState
{
    private PdfRenderingIntent _renderingIntent = PdfRenderingIntent.RelativeColorimetric;

    private PdfColorSpaceConverter _strokeColorConverter;
    private PdfDeviceColorMapping _strokeDeviceColorMapping;
    private ColorTransformSampler? _strokeRgbaSampler;

    private PdfColorSpaceConverter _fillColorConverter;
    private PdfDeviceColorMapping _fillDeviceColorMapping;
    private ColorTransformSampler? _fillRgbaSampler;

    private TransferFunctionTransform? _transferFunction;

    private bool _useBlackPointCompensation = true;

    /// <summary>
    /// Initializes a new graphics state for the given content host.
    /// </summary>
    /// <param name="stateHost">The content host this graphics state is associated with.</param>
    /// <param name="clipBounds">Initial clip bounds.</param>
    /// <param name="recursionGuard">Guard set used to detect and break XObject recursion cycles.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    internal PdfGraphicsState(IPdfContentHost stateHost, in PdfRectangle clipBounds, HashSet<uint> recursionGuard, IPdfExecutionObserver? observer, PdfRenderingParameters renderingParameters)
    {
        Host = stateHost ?? throw new ArgumentNullException(nameof(stateHost));
        RecursionGuard = recursionGuard ?? throw new ArgumentNullException(nameof(recursionGuard));
        ExecutionObserver = observer;
        RenderingParameters = renderingParameters ?? throw new ArgumentNullException(nameof(renderingParameters));
        _fillColorConverter = PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
        _fillDeviceColorMapping = stateHost.Resources.DeviceColorMapping;
        _strokeColorConverter = PdfColorSpaceResolver.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
        _strokeDeviceColorMapping = stateHost.Resources.DeviceColorMapping;
        ClipBounds = clipBounds;

        if (stateHost.Resources.DeviceColorMapping.Gray != null)
        {
            FillPaint = FillPaint.WithSolidColor(FillRgbaSampler.Sample(_fillColorConverter.GetInitialComponents()).ToPdfColor());
            StrokePaint = StrokePaint.WithSolidColor(StrokeRgbaSampler.Sample(_strokeColorConverter.GetInitialComponents()).ToPdfColor());
        }
    }

    internal PdfGraphicsState(IPdfContentHost stateHost, in PdfRectangle clipBounds, PdfGraphicsState sourceState)
        : this(stateHost, clipBounds, sourceState.RecursionGuard, sourceState.ExecutionObserver, sourceState.RenderingParameters)
    {
    }

    /// <summary>
    /// Content host associated with this graphics state (needed for resource lookups, etc.).
    /// </summary>
    internal IPdfContentHost Host { get; }

    /// <summary>
    /// Recursion guard to prevent infinite loops.
    /// </summary>
    public HashSet<uint> RecursionGuard { get; }

    /// <summary>
    /// Observer to notify on PDF processing that some work has been done.
    /// </summary>
    public IPdfExecutionObserver? ExecutionObserver { get; }

    /// <summary>
    /// Parameters for PDF page rendering.
    /// </summary>
    public PdfRenderingParameters RenderingParameters { get; }

    /// <summary>
    /// Current stroking paint (solid color or pattern).
    /// </summary>
    public PdfPaint StrokePaint { get; set; } = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Stroke);

    /// <summary>
    /// Current non-stroking (fill) paint (solid color or pattern).
    /// </summary>
    public PdfPaint FillPaint { get; set; } = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Fill);

    /// <summary>
    /// Gets the ColorTransformSampler for fill operations, cached and invalidated as needed.
    /// </summary>
    public ColorTransformSampler FillRgbaSampler
    {
        get
        {
            _fillRgbaSampler ??= FillColorConverter.GetRgbaSampler(new PdfColorConversionParameters(this, FillDeviceColorMapping));

            return _fillRgbaSampler;
        }
    }

    /// <summary>
    /// Gets the ColorTransformSampler for stroke operations, cached and invalidated as needed.
    /// </summary>
    public ColorTransformSampler StrokeRgbaSampler
    {
        get
        {
            _strokeRgbaSampler ??= StrokeColorConverter.GetRgbaSampler(new PdfColorConversionParameters(this, StrokeDeviceColorMapping));
            return _strokeRgbaSampler;
        }
    }

    /// <summary>
    /// Rendering intent (ri operator). Defaults to RelativeColorimetric per spec.
    /// </summary>
    public PdfRenderingIntent RenderingIntent
    {
        get => _renderingIntent;

        set
        {
            if (_renderingIntent != value)
            {
                _renderingIntent = value;
                InvalidateRgbaSamplers();
            }
        }
    }

    /// <summary>
    /// Current color space converter for stroking operations.
    /// </summary>
    public PdfColorSpaceConverter StrokeColorConverter
    {
        get => _strokeColorConverter;

        set
        {
            if (_strokeColorConverter != value)
            {
                _strokeColorConverter = value;
                _strokeRgbaSampler = null;
            }
        }
    }

    /// <summary>
    /// Color spaces device color values of the stroking color space are interpreted in, taken from the
    /// resources the stroking color space was selected with.
    /// </summary>
    internal PdfDeviceColorMapping StrokeDeviceColorMapping
    {
        get => _strokeDeviceColorMapping;

        set
        {
            if (!_strokeDeviceColorMapping.Equals(value))
            {
                _strokeDeviceColorMapping = value;
                _strokeRgbaSampler = null;
            }
        }
    }

    /// <summary>
    /// Current color space converter for non-stroking (fill) operations.
    /// </summary>
    public PdfColorSpaceConverter FillColorConverter
    {
        get => _fillColorConverter;

        set
        {
            if (_fillColorConverter != value)
            {
                _fillColorConverter = value;
                _fillRgbaSampler = null;
            }
        }
    }

    /// <summary>
    /// Color spaces device color values of the non-stroking (fill) color space are interpreted in, taken from the
    /// resources the fill color space was selected with.
    /// </summary>
    internal PdfDeviceColorMapping FillDeviceColorMapping
    {
        get => _fillDeviceColorMapping;

        set
        {
            if (!_fillDeviceColorMapping.Equals(value))
            {
                _fillDeviceColorMapping = value;
                _fillRgbaSampler = null;
            }
        }
    }

    /// <summary>
    /// Optional transfer function (TR) applied to device output prior to soft mask input or blending.
    /// </summary>
    internal TransferFunctionTransform? TransferFunction
    {
        get => _transferFunction;

        set
        {
            if (_transferFunction != value)
            {
                _transferFunction = value;
                InvalidateRgbaSamplers();
            }
        }
    }

    /// <summary>
    /// Whether black point compensation is applied in color conversions. Defaults to true.
    /// </summary>
    public bool UseBlackPointCompensation
    {
        get => _useBlackPointCompensation;

        set
        {
            if (_useBlackPointCompensation != value)
            {
                _useBlackPointCompensation = value;
                InvalidateRgbaSamplers();
            }
        }
    }

    /// <summary>
    /// Flatness tolerance (i operator). Controls curve flattening accuracy for path rendering.
    /// Default is 1.0 per PDF specification.
    /// </summary>
    public float FlatnessTolerance { get; set; } = 1.0f;

    /// <summary>
    /// Active soft mask (SMask entry in ExtGState) or null when none.
    /// </summary>
    public PdfSoftMask? SoftMask { get; set; }

    /// <summary>
    /// CTM in effect when the <c>gs</c> operator assigned <see cref="SoftMask"/>. Per spec, the soft mask's
    /// BBox/Matrix map through this frozen CTM, not the CTM in effect when the mask is later applied to painted content.
    /// </summary>
    public PdfMatrix SoftMaskCTM { get; set; } = PdfMatrix.Identity;

    /// <summary>
    /// Alpha-is-shape flag (AIS entry in ExtGState). When true, alpha is treated as shape, not opacity.
    /// Default false.
    /// </summary>
    public bool AlphaIsShape { get; set; }

    /// <summary>
    /// Mask rendering mode used internally while rendering soft mask content streams.
    /// </summary>
    public PdfMaskRenderMode MaskRenderMode { get; set; } = PdfMaskRenderMode.None;

    /// <summary>
    /// Knockout flag (TK). Default false.
    /// </summary>
    public bool Knockout { get; set; }

    /// <summary>
    /// Overprint mode (OPM). Default 0.
    /// </summary>
    public int OverprintMode { get; set; }

    // --------------------------------------------------------------------------------------
    // Text state (see PDF 2.0 spec 9 Text) - tracked between BT/ET
    // --------------------------------------------------------------------------------------
    /// <summary>
    /// Current font resource name from Tf operator or external graphics state.
    /// </summary>
    public PdfFont? CurrentFont { get; set; }

    /// <summary>
    /// Font size (Tf operator). Default 1.
    /// </summary>
    public float FontSize { get; set; } = 1f;

    /// <summary>
    /// Character spacing (Tc). Default 0.
    /// </summary>
    public float CharacterSpacing { get; set; }

    /// <summary>
    /// Word spacing (Tw). Default 0.
    /// </summary>
    public float WordSpacing { get; set; }

    /// <summary>
    /// Horizontal scaling (Tz). Stored as percentage (100 = 100%). Default 100.
    /// </summary>
    public float HorizontalScaling { get; set; } = 100f;

    /// <summary>
    /// Text leading (TL). Default 0.
    /// </summary>
    public float Leading { get; set; }

    /// <summary>
    /// Text rise (Ts). Default 0.
    /// </summary>
    public float Rise { get; set; }

    /// <summary>
    /// Text rendering mode (Tr). Default Fill.
    /// </summary>
    public PdfTextRenderingMode TextRenderingMode { get; set; } = PdfTextRenderingMode.Fill;

    /// <summary>
    /// Current text matrix (Tm).
    /// </summary>
    public PdfMatrix TextMatrix { get; set; } = PdfMatrix.Identity;

    /// <summary>
    /// Current text line matrix (start of line position).
    /// </summary>
    public PdfMatrix TextLineMatrix { get; set; } = PdfMatrix.Identity;

    /// <summary>
    /// Current transformation matrix (CTM) from user space to device space.
    /// Stored to enable proper coordinate system transformations for patterns and other operations.
    /// </summary>
    public PdfMatrix CTM { get; set; } = PdfMatrix.Identity;

    /// <summary>
    /// Bounding box of the current clipping path, in the same space <see cref="CTM"/> maps to, starting
    /// at the clip bounds given on construction. A non-rectangular clip contributes the bounds of its path, so this is
    /// always a superset of the area the clip actually keeps.
    /// </summary>
    public PdfRectangle ClipBounds { get; set; }

    /// <summary>
    /// Gets or sets the clipping path used to define the area where text can be rendered.
    /// </summary>
    public PdfPathBuilder? TextClipPath { get; set; }

    /// <summary>
    /// Narrows <see cref="ClipBounds"/> by <paramref name="bounds"/>, given in current user space.
    /// </summary>
    public void IntersectClipBounds(in PdfRectangle bounds)
        => ClipBounds = PdfRectangle.Intersect(ClipBounds, CTM.MapRect(bounds));

    /// <summary>
    /// Returns <see cref="ClipBounds"/> mapped back into current user space, which is the area content
    /// with no extent of its own - a shading that declared no bounding box, a glyph whose font reports
    /// none - can still reach.
    /// </summary>
    public PdfRectangle GetUserSpaceClipBounds() => CTM.Invert().MapRect(ClipBounds);

    /// <summary>
    /// Create a copy for stack push (q operator).
    /// </summary>
    public PdfGraphicsState Clone() => CloneForHost(Host);

    /// <summary>
    /// Create a copy that resolves resources against <paramref name="stateHost"/>, for a nested content
    /// stream that carries its own resource dictionary while inheriting the caller's graphics state.
    /// </summary>
    /// <param name="stateHost">The content host the copy resolves resources against.</param>
    /// <returns>The copied graphics state.</returns>
    internal PdfGraphicsState CloneForHost(IPdfContentHost stateHost) => Copy(stateHost, RenderingParameters);

    /// <summary>
    /// Create a copy for the glyph description of a Type 3 character shown in this state. The glyph inherits the
    /// graphics state aside from the CTM, which starts in glyph space; the soft mask is left out because it applies
    /// to the shown text as a whole.
    /// </summary>
    /// <param name="glyphHost">The content host the glyph description resolves resources against.</param>
    /// <returns>The graphics state the glyph description starts in.</returns>
    internal PdfGraphicsState CloneForType3Glyph(IPdfContentHost glyphHost)
    {
        PdfGraphicsState glyphState = Copy(glyphHost, RenderingParameters.ForType3Rendering());
        glyphState.CTM = PdfMatrix.Identity;
        glyphState.SoftMask = null;
        glyphState.SoftMaskCTM = PdfMatrix.Identity;

        return glyphState;
    }

    /// <summary>
    /// Overwrites this state with the values of <paramref name="source"/> that q saves.
    /// </summary>
    /// <param name="source">The state to copy from.</param>
    internal void CopyFrom(PdfGraphicsState source)
    {
        _renderingIntent = source._renderingIntent;
        _strokeColorConverter = source._strokeColorConverter;
        _strokeDeviceColorMapping = source._strokeDeviceColorMapping;
        _strokeRgbaSampler = source._strokeRgbaSampler;
        _fillColorConverter = source._fillColorConverter;
        _fillDeviceColorMapping = source._fillDeviceColorMapping;
        _fillRgbaSampler = source._fillRgbaSampler;
        _transferFunction = source._transferFunction;
        _useBlackPointCompensation = source._useBlackPointCompensation;
        StrokePaint = source.StrokePaint;
        FillPaint = source.FillPaint;
        FlatnessTolerance = source.FlatnessTolerance;
        SoftMask = source.SoftMask;
        SoftMaskCTM = source.SoftMaskCTM;
        AlphaIsShape = source.AlphaIsShape;
        OverprintMode = source.OverprintMode;
        CurrentFont = source.CurrentFont;
        FontSize = source.FontSize;
        CharacterSpacing = source.CharacterSpacing;
        WordSpacing = source.WordSpacing;
        HorizontalScaling = source.HorizontalScaling;
        Leading = source.Leading;
        Rise = source.Rise;
        TextRenderingMode = source.TextRenderingMode;
        TextMatrix = source.TextMatrix;
        TextLineMatrix = source.TextLineMatrix;
        CTM = source.CTM;
        ClipBounds = source.ClipBounds;
        TextClipPath = source.TextClipPath;
    }

    private PdfGraphicsState Copy(IPdfContentHost stateHost, PdfRenderingParameters renderingParameters)
    {
        PdfGraphicsState copy = new(stateHost, ClipBounds, RecursionGuard, ExecutionObserver, renderingParameters);
        copy.CopyFrom(this);

        return copy;
    }

    private void InvalidateRgbaSamplers()
    {
        _fillRgbaSampler = null;
        _strokeRgbaSampler = null;
    }
}
