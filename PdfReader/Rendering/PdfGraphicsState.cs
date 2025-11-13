using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;

namespace PdfReader.Rendering
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
        /// <summary>
        /// Identity matrix shortcut used for initializing text matrices, etc.
        /// </summary>
        public static SKMatrix IdentityMatrix = SKMatrix.Identity;

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
        /// Rendering intent (ri operator). Defaults to RelativeColorimetric per spec.
        /// </summary>
        public PdfRenderingIntent RenderingIntent { get; set; } = PdfRenderingIntent.RelativeColorimetric;

        /// <summary>
        /// Current color space converter for stroking operations.
        /// </summary>
        public PdfColorSpaceConverter StrokeColorConverter { get; set; } = DeviceRgbConverter.Instance;

        /// <summary>
        /// Current color space converter for non-stroking (fill) operations.
        /// </summary>
        public PdfColorSpaceConverter FillColorConverter { get; set; } = DeviceRgbConverter.Instance;

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
        /// Current font resource name (from Tf operator) or null.
        /// </summary>
        public PdfString CurrentFont { get; set; }

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
        public SKMatrix TextMatrix { get; set; } = IdentityMatrix;

        /// <summary>
        /// Current text line matrix (start of line position).
        /// </summary>
        public SKMatrix TextLineMatrix { get; set; } = IdentityMatrix;

        /// <summary>
        /// Current transformation matrix (CTM) from user space to device space.
        /// Stored to enable proper coordinate system transformations for patterns and other operations.
        /// </summary>
        public SKMatrix CTM { get; set; } = IdentityMatrix;

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
            return new PdfGraphicsState
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
                TextClipPath = TextClipPath
            };
        }
    }
}