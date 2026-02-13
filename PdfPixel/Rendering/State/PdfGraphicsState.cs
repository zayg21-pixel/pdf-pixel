using SkiaSharp;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Text;
using PdfPixel.Color.Paint;
using PdfPixel.Transparency.Model;
using PdfPixel.Fonts.Model;
using PdfPixel.Models;
using System.Collections.Generic;
using System;
using PdfPixel.Color.Transform;
using PdfPixel.Color.Sampling;

namespace PdfPixel.Rendering.State
{
    public enum PdfMaskRenderMode
    {
        None,
        Alpha,
        Luminosity
    }

    /// <summary>
    /// Graphics state for PDF rendering - corresponds to the PDF graphics state stack (q/Q operators).
    /// NOTE: Keep property comments in sync with PDF spec sections for easier maintenance.
    /// </summary>
    public class PdfGraphicsState
    {
        private IRgbaSampler _fillRgbaSampler;
        private IRgbaSampler _strokeRgbaSampler;
        private IColorTransform _fullTransferFunction;

        private PdfRenderingIntent _renderingIntent = PdfRenderingIntent.RelativeColorimetric;
        private PdfColorSpaceConverter _strokeColorConverter;
        private PdfColorSpaceConverter _fillColorConverter;
        private TransferFunctionTransform _transferFunction;
        private IColorTransform _externalTransferFunction;

        public PdfGraphicsState(PdfPage statePage, HashSet<uint> recursionGuard, PdfRenderingParameters renderingParameters, IColorTransform externalTransform)
        {
            Page = statePage ?? throw new ArgumentNullException(nameof(renderingParameters));
            ExternalTransferFunction = externalTransform;
            RecursionGuard = recursionGuard ?? throw new ArgumentNullException(nameof(renderingParameters));
            RenderingParameters = renderingParameters ?? throw new ArgumentNullException(nameof(renderingParameters));
            FillColorConverter = statePage.Cache.ColorSpace.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
            StrokeColorConverter = statePage.Cache.ColorSpace.ResolveDeviceConverter(PdfColorSpaceType.DeviceGray);
        }

        /// <summary>
        /// Page associated with this graphics state (needed for resource lookups, etc.).
        /// </summary>
        public PdfPage Page { get; }

        /// <summary>
        /// Recursion guard to prevent infinite loops.
        /// </summary>
        public HashSet<uint> RecursionGuard { get; }

        /// <summary>
        /// Rendering parameters of a current graphics state.
        /// </summary>
        public PdfRenderingParameters RenderingParameters { get; }

        // --------------------------------------------------------------------------------------
        // Consolidated paint objects (new API)
        // --------------------------------------------------------------------------------------
        /// <summary>
        /// Current stroking paint (solid color or pattern). Replaces legacy StrokeColor / StrokePattern* fields.
        /// </summary>
        public PdfPaint StrokePaint { get; set; } = PdfPaint.Solid(SKColors.Black);

        /// <summary>
        /// Current non-stroking (fill) paint (solid color or pattern). Replaces legacy FillColor / FillPattern* fields.
        /// </summary>
        public PdfPaint FillPaint { get; set; } = PdfPaint.Solid(SKColors.Black);

        /// <summary>
        /// Gets the IRgbaSampler for fill operations, cached and invalidated as needed.
        /// </summary>
        public IRgbaSampler FillRgbaSampler
        {
            get
            {
                _fillRgbaSampler ??= FillColorConverter.GetRgbaSampler(RenderingIntent, FullTransferFunction);

                return _fillRgbaSampler;
            }
        }

        /// <summary>
        /// Gets the IRgbaSampler for stroke operations, cached and invalidated as needed.
        /// </summary>
        public IRgbaSampler StrokeRgbaSampler
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
            get { return _fillColorConverter; }
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
        public TransferFunctionTransform TransferFunction
        {
            get { return _transferFunction; }
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
        public IColorTransform ExternalTransferFunction
        {
            get { return _externalTransferFunction; }
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
        public IColorTransform FullTransferFunction
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
        // Line state (see PDF 2.0 spec 8.4 Graphics State)
        // --------------------------------------------------------------------------------------
        /// <summary>
        /// Line width (w operator). Default 1.
        /// </summary>
        public float LineWidth { get; set; } = 1.0f;

        /// <summary>
        /// Line cap style (J operator). Default Butt.
        /// </summary>
        public SKStrokeCap LineCap { get; set; } = SKStrokeCap.Butt;

        /// <summary>
        /// Line join style (j operator). Default Miter.
        /// </summary>
        public SKStrokeJoin LineJoin { get; set; } = SKStrokeJoin.Miter;

        /// <summary>
        /// Miter limit (M operator). Default 10.
        /// </summary>
        public float MiterLimit { get; set; } = 10.0f;

        /// <summary>
        /// Dash pattern array (d operator). Null means solid line.
        /// </summary>
        public float[] DashPattern { get; set; }

        /// <summary>
        /// Dash phase (d operator). Default 0.
        /// </summary>
        public float DashPhase { get; set; } = 0.0f;

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
        /// Stroke alpha constant (CA entry in ExtGState). 0 = fully transparent, 1 = opaque.
        /// </summary>
        public float StrokeAlpha { get; set; } = 1.0f;

        /// <summary>
        /// Fill alpha constant (ca entry in ExtGState). 0 = fully transparent, 1 = opaque.
        /// </summary>
        public float FillAlpha { get; set; } = 1.0f;

        /// <summary>
        /// Current blend mode (BM entry in ExtGState). Default Normal.
        /// </summary>
        public PdfBlendMode BlendMode { get; set; } = PdfBlendMode.Normal;

        /// <summary>
        /// Active soft mask (SMask entry in ExtGState) or null when none.
        /// </summary>
        public PdfSoftMask SoftMask { get; set; }

        /// <summary>
        /// Alpha-is-shape flag (AIS entry in ExtGState). When true, alpha is treated as shape, not opacity.
        /// Default false.
        /// </summary>
        public bool AlphaIsShape { get; set; } = false;

        /// <summary>
        /// Gets or sets the transformation matrix applied to the device.
        /// </summary>
        public SKMatrix DeviceMatrix { get; set; } = SKMatrix.Identity;

        /// <summary>
        /// Mask rendering mode used internally while rendering soft mask content streams.
        /// </summary>
        public PdfMaskRenderMode MaskRenderMode { get; set; } = PdfMaskRenderMode.None;

        /// <summary>
        /// Knockout flag (TK). Default false.
        /// </summary>
        public bool Knockout { get; set; } = false;

        /// <summary>
        /// Overprint mode (OPM). Default 0.
        /// </summary>
        public int OverprintMode { get; set; } = 0;

        /// <summary>
        /// Overprint flag for stroke operations (OP). Default false.
        /// </summary>
        public bool OverprintStroke { get; set; } = false;

        /// <summary>
        /// Overprint flag for fill operations (op). Default false.
        /// </summary>
        public bool OverprintFill { get; set; } = false;

        // --------------------------------------------------------------------------------------
        // Text state (see PDF 2.0 spec 9 Text) - tracked between BT/ET
        // --------------------------------------------------------------------------------------
        /// <summary>
        /// Current font resource name from Tf operator or external graphics state.
        /// </summary>
        public PdfFontBase CurrentFont { get; set; }

        /// <summary>
        /// Font size (Tf operator). Default 1.
        /// </summary>
        public float FontSize { get; set; } = 1f;

        /// <summary>
        /// Character spacing (Tc). Default 0.
        /// </summary>
        public float CharacterSpacing { get; set; } = 0f;

        /// <summary>
        /// Word spacing (Tw). Default 0.
        /// </summary>
        public float WordSpacing { get; set; } = 0f;

        /// <summary>
        /// Horizontal scaling (Tz). Stored as percentage (100 = 100%). Default 100.
        /// </summary>
        public float HorizontalScaling { get; set; } = 100f;

        /// <summary>
        /// Text leading (TL). Default 0.
        /// </summary>
        public float Leading { get; set; } = 0f;

        /// <summary>
        /// Text rise (Ts). Default 0.
        /// </summary>
        public float Rise { get; set; } = 0f;

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
        public bool InTextObject { get; set; } = false;

        /// <summary>
        /// Gets or sets the clipping path used to define the area where text can be rendered.
        /// </summary>
        public SKPath TextClipPath { get; set; }

        /// <summary>
        /// Create a deep copy for stack push (q operator). Paint objects are reference-copied (immutable usage expected).
        /// </summary>
        public PdfGraphicsState Clone()
        {
            return new PdfGraphicsState(Page, RecursionGuard, RenderingParameters, ExternalTransferFunction)
            {
                StrokePaint = StrokePaint,
                FillPaint = FillPaint,
                StrokeAlpha = StrokeAlpha,
                FlatnessTolerance = FlatnessTolerance,
                FillAlpha = FillAlpha,
                BlendMode = BlendMode,
                LineWidth = LineWidth,
                LineCap = LineCap,
                LineJoin = LineJoin,
                MiterLimit = MiterLimit,
                DashPattern = DashPattern != null ? (float[])DashPattern.Clone() : null,
                DashPhase = DashPhase,
                StrokeColorConverter = StrokeColorConverter,
                FillColorConverter = FillColorConverter,
                RenderingIntent = RenderingIntent,
                SoftMask = SoftMask,
                TransferFunction = TransferFunction,
                AlphaIsShape = AlphaIsShape,
                OverprintMode = OverprintMode,
                OverprintStroke = OverprintStroke,
                OverprintFill = OverprintFill,
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
                DeviceMatrix = DeviceMatrix,
                TextClipPath = TextClipPath,
            };
        }

        private void InvalidateRgbaSamplers()
        {
            _fillRgbaSampler = null;
            _strokeRgbaSampler = null;
        }
    }
}