using PdfPixel.Color.Paint;
using PdfPixel.Fonts;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Geometry;
using System;
using PdfPixel.Color;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Transparency;

namespace PdfPixel.Rendering;

/// <summary>
/// Container for optionally parsed graphics state parameters from an ExtGState dictionary.
/// All properties are nullable to indicate absence in the source dictionary. Apply using <see cref="ApplyToGraphicsState"/>.
/// </summary>
internal sealed class PdfGraphicsStateParameters
{
    /// <summary>
    /// Reads the graphics state parameters of the ExtGState <paramref name="dictionary"/>.
    /// </summary>
    /// <param name="dictionary">The ExtGState dictionary.</param>
    public PdfGraphicsStateParameters(PdfDictionary dictionary)
    {
        AlphaIsShape = dictionary.GetBoolean(PdfTokens.AlphaIsShapeKey);
        LineWidth = dictionary.GetFloat(PdfTokens.LineWidthKey);
        MiterLimit = dictionary.GetFloat(PdfTokens.MiterLimitKey);

        float? capStyle = dictionary.GetFloat(PdfTokens.LineCapKey);
        if (capStyle.HasValue)
        {
            LineCap = capStyle.Value switch
            {
                0 => PdfStrokeCap.Butt,
                1 => PdfStrokeCap.Round,
                2 => PdfStrokeCap.Square,
                _ => PdfStrokeCap.Butt
            };
        }

        float? joinStyle = dictionary.GetFloat(PdfTokens.LineJoinKey);
        if (joinStyle.HasValue)
        {
            LineJoin = joinStyle.Value switch
            {
                0 => PdfStrokeJoin.Miter,
                1 => PdfStrokeJoin.Round,
                2 => PdfStrokeJoin.Bevel,
                _ => PdfStrokeJoin.Miter
            };
        }

        PdfArray? dashArray = dictionary.GetArray(PdfTokens.DashPatternKey);
        if (dashArray?.Count >= 2)
        {
            float[]? patternArray = dashArray.GetArray(0)?.GetFloatArray();
            float phase = dashArray.GetFloatOrDefault(1);

            if (patternArray?.Length > 0)
            {
                DashPattern = patternArray;
                DashPhase = phase;
            }
            else
            {
                // Empty array means solid line
                DashPattern = null;
                DashPhase = 0f;
            }
        }

        float? strokeAlpha = dictionary.GetFloat(PdfTokens.StrokeAlphaKey); // Stroke alpha (/CA)
        if (strokeAlpha.HasValue)
        {
            // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
            StrokeAlpha = Math.Max(0f, Math.Min(1f, strokeAlpha.Value));
        }

        float? fillAlpha = dictionary.GetFloat(PdfTokens.FillAlphaKey); // Fill alpha (/ca)
        if (fillAlpha.HasValue)
        {
            // Clamp alpha to valid range [0.0, 1.0] as per PDF specification
            FillAlpha = Math.Max(0f, Math.Min(1f, fillAlpha.Value));
        }

        if (dictionary.HasKey(PdfTokens.BlendModeKey))
        {
            var mode = PdfBlendMode.Unknown;

            // First try to get as name
            PdfString? blendModeName = dictionary.GetName(PdfTokens.BlendModeKey);
            if (blendModeName != null)
            {
                mode = blendModeName.Value.AsEnum<PdfBlendMode>();
            }
            else
            {
                // Handle blend mode arrays - PDF viewers should use the first supported blend mode
                PdfArray? blendModeArray = dictionary.GetArray(PdfTokens.BlendModeKey);
                if (blendModeArray != null)
                {
                    for (int index = 0; index < blendModeArray.Count; index++)
                    {
                        PdfBlendMode candidate = blendModeArray.GetNameOrDefault(index).AsEnum<PdfBlendMode>();
                        if (candidate != PdfBlendMode.Unknown)
                        {
                            mode = candidate;
                            break;
                        }
                    }
                }
            }

            // An unrecognized blend mode name falls back to Normal, per spec, rather than being ignored.
            BlendMode = (mode == PdfBlendMode.Unknown) ? PdfBlendMode.Normal : mode;
        }

        // Custom transformation matrix (/Matrix, or /CTM as an alternative key name)
        PdfArray? matrixArray = dictionary.GetArray(PdfTokens.MatrixKey) ?? dictionary.GetArray(PdfTokens.CTMKey);
        TransformMatrix = PdfMatrix.FromArray(matrixArray);

        // Soft Mask (/SMask)
        if (dictionary.HasKey(PdfTokens.SoftMaskKey))
        {
            PdfDictionary? softMaskDict = dictionary.GetDictionary(PdfTokens.SoftMaskKey);
            if (softMaskDict != null)
            {
                SoftMask = PdfSoftMask.FromDictionary(softMaskDict);
            }
            else if (dictionary.GetName(PdfTokens.SoftMaskKey) == PdfTokens.NoneValue)
            {
                ShouldUnsetSoftMask = true;
            }
        }

        // Transfer Function (/TR2 takes priority over /TR); the unset name (/Identity for TR,
        // /Default for TR2) clears any inherited transfer function.
        if (dictionary.HasKey(PdfTokens.TransferFunction2Key))
        {
            TransferFunction = TransferFunctionTransform.GetTransferFunction(dictionary, PdfTokens.TransferFunction2Key);
            if (TransferFunction == null && dictionary.GetName(PdfTokens.TransferFunction2Key) == PdfTokens.DefaultValue)
            {
                ShouldUnsetTransferFunction = true;
            }
        }
        else if (dictionary.HasKey(PdfTokens.TransferFunctionKey))
        {
            TransferFunction = TransferFunctionTransform.GetTransferFunction(dictionary, PdfTokens.TransferFunctionKey);
            if (TransferFunction == null && dictionary.GetName(PdfTokens.TransferFunctionKey) == PdfTokens.IdentityKey)
            {
                ShouldUnsetTransferFunction = true;
            }
        }

        Knockout = dictionary.GetBoolean(PdfTokens.KnockoutKey);           // Knockout (/TK)
        OverprintMode = dictionary.GetInteger(PdfTokens.OverprintModeKey); // Overprint Mode (/OPM)
        OverprintStroke = dictionary.GetBoolean(PdfTokens.OverprintStrokeKey); // Overprint Stroke (/OP)
        OverprintFill = dictionary.GetBoolean(PdfTokens.OverprintFillKey);     // Overprint Fill (/op)

        // Font (/Font)
        PdfArray? fontArray = dictionary.GetArray(PdfTokens.FontKey);
        if (fontArray?.Count == 2)
        {
            float fontSize = fontArray.GetFloatOrDefault(1);

            Font = PdfFont.GetFont(fontArray, 0);
            FontSize = fontSize;
        }

        // Rendering intent (/RI)
        if (dictionary.HasKey(PdfTokens.StateIntentKey))
        {
            PdfRenderingIntent = dictionary.GetNameOrDefault(PdfTokens.StateIntentKey).AsEnum<PdfRenderingIntent>();
        }

        // Black point compensation (/UseBlackPtComp)
        if (dictionary.HasKey(PdfTokens.UseBlackPtCompKey))
        {
            UseBlackPtComp = dictionary.GetNameOrDefault(PdfTokens.UseBlackPtCompKey).AsEnum<PdfBlackPointCompensation>();
        }
    }

    /// <summary>
    /// Parsed line width (w operator). Null when not specified. Default (if applied and null) is1 per PDF spec.
    /// </summary>
    public float? LineWidth { get; }

    /// <summary>
    /// Parsed line cap style (LineCap key / LC operator). Null when absent.
    /// </summary>
    public PdfStrokeCap? LineCap { get; }

    /// <summary>
    /// Parsed line join style (LineJoin key / LJ operator). Null when absent.
    /// </summary>
    public PdfStrokeJoin? LineJoin { get; }

    /// <summary>
    /// Parsed miter limit (MiterLimit key / ML operator). Null when absent.
    /// </summary>
    public float? MiterLimit { get; }

    /// <summary>
    /// Parsed dash pattern array (from DashPattern key / D operator). Null or empty indicates solid line.
    /// </summary>
    public float[]? DashPattern { get; }

    /// <summary>
    /// Parsed dash phase (second element of D operator array). Null when absent.
    /// </summary>
    public float? DashPhase { get; }

    /// <summary>
    /// Parsed stroke alpha constant (/CA). Clamped to [0,1]. Null when absent.
    /// </summary>
    public float? StrokeAlpha { get; }

    /// <summary>
    /// Parsed fill alpha constant (/ca). Clamped to [0,1]. Null when absent.
    /// </summary>
    public float? FillAlpha { get; }

    /// <summary>
    /// Parsed blend mode (/BM). Null when absent or unsupported (defaults to Normal in graphics state).
    /// </summary>
    public PdfBlendMode? BlendMode { get; }

    /// <summary>
    /// Parsed transformation matrix (/Matrix or /CTM). Null when not present.
    /// </summary>
    public PdfMatrix? TransformMatrix { get; }

    /// <summary>
    /// Parsed soft mask (/SMask). Null when absent or /None.
    /// </summary>
    public PdfSoftMask? SoftMask { get; }

    /// <summary>
    /// True when /SMask was explicitly set to /None, meaning any inherited soft mask must be cleared.
    /// </summary>
    public bool ShouldUnsetSoftMask { get; }

    /// <summary>
    /// Parsed knockout flag (/TK). Null when absent.
    /// </summary>
    public bool? Knockout { get; }

    /// <summary>
    /// Parsed overprint mode (/OPM). Null when absent.
    /// </summary>
    public int? OverprintMode { get; }

    /// <summary>
    /// Parsed overprint stroke flag (/OP). Null when absent.
    /// </summary>
    public bool? OverprintStroke { get; }

    /// <summary>
    /// Parsed overprint fill flag (/op). Null when absent.
    /// </summary>
    public bool? OverprintFill { get; }

    /// <summary>
    /// Parsed font (/Font). Null when absent.
    /// </summary>
    public PdfFont? Font { get; }

    /// <summary>
    /// Parsed font size from (/Font). Null when absent.
    /// </summary>
    public float? FontSize { get; }

    /// <summary>
    /// Parsed rendering intent (/RI). Null when absent.
    /// </summary>
    public PdfRenderingIntent? PdfRenderingIntent { get; }

    /// <summary>
    /// Parsed black point compensation setting (/UseBlackPtComp). Null when absent.
    /// </summary>
    public PdfBlackPointCompensation? UseBlackPtComp { get; }

    /// <summary>
    /// Parsed alpha-is-shape flag (/AIS). Null when absent.
    /// </summary>
    public bool? AlphaIsShape { get; }

    /// <summary>
    /// Optional transfer function (TR or TR2) parsed from ExtGState.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// True when TR was explicitly set to /Identity, or TR2 to /Default, meaning any inherited transfer function must be cleared.
    /// </summary>
    public bool ShouldUnsetTransferFunction { get; }

    /// <summary>
    /// The graphics state parameters of the ExtGState dictionary held by <paramref name="key"/> of
    /// <paramref name="dictionary"/>, or <see langword="null"/> when absent or not a dictionary.
    /// </summary>
    /// <param name="dictionary">Dictionary holding the ExtGState entry.</param>
    /// <param name="key">Key of the ExtGState entry.</param>
    public static PdfGraphicsStateParameters? GetGraphicsStateParameters(PdfDictionary dictionary, in PdfString key)
        => dictionary.Document.ObjectCache.GetOrCreate(dictionary, key, FromObject);

    /// <summary>
    /// Apply parsed parameter values to a target graphics state instance. Only non-null entries are applied.
    /// </summary>
    /// <param name="graphicsState">Target graphics state.</param>
    public void ApplyToGraphicsState(PdfGraphicsState graphicsState)
    {
        if (graphicsState == null)
        {
            return;
        }

        graphicsState.StrokePaint = graphicsState.StrokePaint.WithGraphicsState(this);
        graphicsState.FillPaint = graphicsState.FillPaint.WithGraphicsState(this);

        if (SoftMask != null)
        {
            graphicsState.SoftMask = SoftMask;
            graphicsState.SoftMaskCTM = graphicsState.CTM;
        }
        else if (ShouldUnsetSoftMask)
        {
            graphicsState.SoftMask = null;
        }

        if (TransferFunction != null)
        {
            graphicsState.TransferFunction = TransferFunction;
        }
        else if (ShouldUnsetTransferFunction)
        {
            graphicsState.TransferFunction = null;
        }

        if (Knockout.HasValue)
        {
            graphicsState.Knockout = Knockout.Value;
        }

        if (OverprintMode.HasValue)
        {
            graphicsState.OverprintMode = OverprintMode.Value;
        }

        if (TransformMatrix.HasValue)
        {
            PdfMatrix matrix = TransformMatrix.Value;
            graphicsState.CTM = matrix.PostConcat(graphicsState.CTM);
        }

        if (Font != null)
        {
            graphicsState.CurrentFont = Font;
        }

        if (FontSize.HasValue)
        {
            graphicsState.FontSize = FontSize.Value;
        }

        if (PdfRenderingIntent.HasValue)
        {
            graphicsState.RenderingIntent = PdfRenderingIntent.Value;
        }

        if (UseBlackPtComp.HasValue)
        {
            graphicsState.UseBlackPointCompensation = UseBlackPtComp.Value != PdfBlackPointCompensation.Off;
        }

        if (AlphaIsShape.HasValue)
        {
            graphicsState.AlphaIsShape = AlphaIsShape.Value;
        }
    }

    /// <summary>
    /// Creates the graphics state parameters of an ExtGState object.
    /// </summary>
    /// <param name="graphicsStateObject">Graphics state dictionary object.</param>
    /// <returns>Parsed parameters, or <see langword="null"/> when the object is not a dictionary.</returns>
    private static PdfGraphicsStateParameters? FromObject(PdfObject graphicsStateObject)
    {
        PdfDictionary? graphicsStateDictionary = graphicsStateObject.Value.AsDictionary();
        if (graphicsStateDictionary == null)
        {
            return null;
        }

        return new PdfGraphicsStateParameters(graphicsStateDictionary);
    }
}
