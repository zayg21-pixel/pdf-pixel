using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using PdfPixel.TextExtraction;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Rendering.State;

/// <summary>
/// Graphics state for PDF rendering - corresponds to the PDF graphics state stack (q/Q operators).
/// </summary>
public class PdfGraphicsState
{
    private IColorTransform? _fullTransferFunction;

    private PdfRenderingIntent _renderingIntent = PdfRenderingIntent.RelativeColorimetric;

    private PdfColorSpaceConverter _strokeColorConverter;
    private ColorTransformSampler? _strokeRgbaSampler;

    private PdfColorSpaceConverter _fillColorConverter;
    private ColorTransformSampler? _fillRgbaSampler;

    private TransferFunctionTransform? _transferFunction;
    private IColorTransform? _externalTransferFunction;

    /// <summary>
    /// Initializes a new graphics state for the given page.
    /// </summary>
    /// <param name="statePage">The page this graphics state is associated with.</param>
    /// <param name="recursionGuard">Guard set used to detect and break XObject recursion cycles.</param>
    /// <param name="externalTransform">Optional external transfer function applied on top of the page-level transfer function.</param>
    /// <param name="observer">Execution observer to notify on long-running operations.</param>
    /// <param name="renderingParameters">Parameters for PDF page rendering.</param>
    internal PdfGraphicsState(IPdfPageInternal statePage, HashSet<uint> recursionGuard, IColorTransform? externalTransform, IPdfExecutionObserver? observer, PdfRenderingParameters renderingParameters)
    {
        Page = statePage ?? throw new ArgumentNullException(nameof(statePage));
        ExternalTransferFunction = externalTransform;
        RecursionGuard = recursionGuard ?? throw new ArgumentNullException(nameof(recursionGuard));
        ExecutionObserver = observer;
        RenderingParameters = renderingParameters ?? throw new ArgumentNullException(nameof(renderingParameters));
        _fillColorConverter = statePage.Cache.ColorSpace.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
        _strokeColorConverter = statePage.Cache.ColorSpace.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
    }

    internal PdfGraphicsState(IPdfPageInternal statePage, PdfGraphicsState sourceState)
        : this(statePage, sourceState.RecursionGuard, sourceState.ExternalTransferFunction, sourceState.ExecutionObserver, sourceState.RenderingParameters)
    {
    }

    /// <summary>
    /// Page associated with this graphics state (needed for resource lookups, etc.).
    /// </summary>
    internal IPdfPageInternal Page { get; }

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
            _fillRgbaSampler ??= FillColorConverter.GetRgbaSampler(RenderingIntent, FullTransferFunction);

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
            _strokeRgbaSampler ??= StrokeColorConverter.GetRgbaSampler(RenderingIntent, FullTransferFunction);
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
    /// Optional transfer function (TR) applied to device output prior to soft mask input or blending.
    /// </summary>
    public TransferFunctionTransform? TransferFunction
    {
        get => _transferFunction;

        set
        {
            if (_transferFunction != value)
            {
                _transferFunction = value;
                _fullTransferFunction = null;
                InvalidateRgbaSamplers();
            }
        }
    }

    /// <summary>
    /// Optional external transfer function (TR) provided from caller.
    /// </summary>
    public IColorTransform? ExternalTransferFunction
    {
        get => _externalTransferFunction;

        set
        {
            if (_externalTransferFunction != value)
            {
                _externalTransferFunction = value;
                _fullTransferFunction = null;
                InvalidateRgbaSamplers();
            }
        }
    }

    /// <summary>
    /// Gets the complete color transfer function by combining the internal and external transfer functions, if both
    /// are available.
    /// </summary>
    public IColorTransform? FullTransferFunction
    {
        get
        {
            if (_fullTransferFunction != null)
            {
                return _fullTransferFunction;
            }

            if (TransferFunction == null && ExternalTransferFunction == null)
            {
                return null;
            }
            else if (TransferFunction != null && ExternalTransferFunction != null)
            {
                _fullTransferFunction = new ChainedColorTransform(TransferFunction, ExternalTransferFunction);
            }
            else
            {
                _fullTransferFunction = TransferFunction ?? ExternalTransferFunction;
            }

            return _fullTransferFunction;
        }
    }

    // --------------------------------------------------------------------------------------
    // Path rendering state (see PDF 2.0 spec 8.4 Graphics State)
    // --------------------------------------------------------------------------------------
    /// <summary>
    /// Flatness tolerance (i operator). Controls curve flattening accuracy for path rendering.
    /// Default is 1.0 per PDF specification.
    /// </summary>
    public float FlatnessTolerance { get; set; } = 1.0f;

    // --------------------------------------------------------------------------------------
    // Transparency state (see PDF 2.0 spec 11 Transparency)
    // --------------------------------------------------------------------------------------
    /// <summary>
    /// Active soft mask (SMask entry in ExtGState) or null when none.
    /// </summary>
    public PdfSoftMask? SoftMask { get; set; }

    /// <summary>
    /// CTM in effect when the <c>gs</c> operator assigned <see cref="SoftMask"/>. Per spec, the soft mask's
    /// BBox/Matrix map through this frozen CTM, not the CTM in effect when the mask is later applied to painted content.
    /// </summary>
    public SKMatrix SoftMaskCTM { get; set; } = SKMatrix.Identity;

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
    public PdfFontBase? CurrentFont { get; set; }

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
    public SKMatrix TextMatrix { get; set; } = SKMatrix.Identity;

    /// <summary>
    /// Current text line matrix (start of line position).
    /// </summary>
    public SKMatrix TextLineMatrix { get; set; } = SKMatrix.Identity;

    /// <summary>
    /// Current transformation matrix (CTM) from user space to device space.
    /// Stored to enable proper coordinate system transformations for patterns and other operations.
    /// </summary>
    public SKMatrix CTM { get; set; } = SKMatrix.Identity;

    /// <summary>
    /// True while inside a text object (between BT and ET).
    /// </summary>
    public bool InTextObject { get; set; }

    /// <summary>
    /// Gets or sets the clipping path used to define the area where text can be rendered.
    /// </summary>
    public SKPathBuilder? TextClipPath { get; set; }

    /// <summary>
    /// Text markup set by a DP (marked content point with properties) operator.
    /// Consumed and cleared by the next text drawing operation.
    /// </summary>
    public PdfTextMarkup? PendingTextMarkup { get; set; }

    /// <summary>
    /// Create a deep copy for stack push (q operator). Paint objects are cloned so mutations inside the
    /// pushed scope never alias the parent scope's paint.
    /// </summary>
    public PdfGraphicsState Clone()
    {
        return new(Page, this)
        {
            StrokePaint = StrokePaint.Clone(),
            FillPaint = FillPaint.Clone(),
            FlatnessTolerance = FlatnessTolerance,
            StrokeColorConverter = StrokeColorConverter,
            FillColorConverter = FillColorConverter,
            RenderingIntent = RenderingIntent,
            SoftMask = SoftMask,
            SoftMaskCTM = SoftMaskCTM,
            TransferFunction = TransferFunction,
            AlphaIsShape = AlphaIsShape,
            OverprintMode = OverprintMode,
            CurrentFont = CurrentFont,
            FontSize = FontSize,
            CharacterSpacing = CharacterSpacing,
            WordSpacing = WordSpacing,
            HorizontalScaling = HorizontalScaling,
            Leading = Leading,
            Rise = Rise,
            TextRenderingMode = TextRenderingMode,
            TextMatrix = TextMatrix,
            TextLineMatrix = TextLineMatrix,
            InTextObject = InTextObject,
            CTM = CTM,
            TextClipPath = TextClipPath
        };
    }

    private void InvalidateRgbaSamplers()
    {
        _fillRgbaSampler = null;
        _strokeRgbaSampler = null;
    }
}
