using SkiaSharp;
using PdfReader.Models;
using System;

namespace PdfReader.Rendering.State
{
    /// <summary>
    /// Container for optionally parsed graphics state parameters from an ExtGState dictionary.
    /// All properties are nullable to indicate absence in the source dictionary. Apply using <see cref="ApplyToGraphicsState"/>.
    /// </summary>
    internal sealed class PdfGraphicsStateParameters
    {
        /// <summary>
        /// Parsed line width (w operator). Null when not specified. Default (if applied and null) is1 per PDF spec.
        /// </summary>
        public float? LineWidth { get; set; }

        /// <summary>
        /// Parsed line cap style (LineCap key / LC operator). Values map to <see cref="SKStrokeCap"/>. Null when absent.
        /// </summary>
        public SKStrokeCap? LineCap { get; set; }

        /// <summary>
        /// Parsed line join style (LineJoin key / LJ operator). Values map to <see cref="SKStrokeJoin"/>. Null when absent.
        /// </summary>
        public SKStrokeJoin? LineJoin { get; set; }

        /// <summary>
        /// Parsed miter limit (MiterLimit key / ML operator). Null when absent.
        /// </summary>
        public float? MiterLimit { get; set; }

        /// <summary>
        /// Parsed dash pattern array (from DashPattern key / D operator). Null or empty indicates solid line.
        /// </summary>
        public float[] DashPattern { get; set; }

        /// <summary>
        /// Parsed dash phase (second element of D operator array). Null when absent.
        /// </summary>
        public float? DashPhase { get; set; }

        /// <summary>
        /// Parsed stroke alpha constant (/CA). Clamped to [0,1]. Null when absent.
        /// </summary>
        public float? StrokeAlpha { get; set; }

        /// <summary>
        /// Parsed fill alpha constant (/ca). Clamped to [0,1]. Null when absent.
        /// </summary>
        public float? FillAlpha { get; set; }

        /// <summary>
        /// Parsed blend mode (/BM). Null when absent or unsupported (defaults to Normal in graphics state).
        /// </summary>
        public PdfBlendMode? BlendMode { get; set; }

        /// <summary>
        /// Parsed transformation matrix (/Matrix or /CTM). Null when not present.
        /// </summary>
        public SKMatrix? TransformMatrix { get; set; }

        /// <summary>
        /// Parsed soft mask (/SMask). Null when absent or /None.
        /// </summary>
        public PdfSoftMask SoftMask { get; set; }

        /// <summary>
        /// Parsed knockout flag (/TK). Null when absent.
        /// </summary>
        public bool? Knockout { get; set; }

        /// <summary>
        /// Parsed overprint mode (/OPM). Null when absent.
        /// </summary>
        public int? OverprintMode { get; set; }

        /// <summary>
        /// Parsed overprint stroke flag (/OP). Null when absent.
        /// </summary>
        public bool? OverprintStroke { get; set; }

        /// <summary>
        /// Parsed overprint fill flag (/op). Null when absent.
        /// </summary>
        public bool? OverprintFill { get; set; }

        // TODO: Font (name and size) not yet parsed (ExtGState /Font entry)
        // TODO: RenderingIntent (/RI) ignored
        // TODO: StrokeAdjust (/SA) ignored
        // TODO: Transfer functions (/TR /TR2) ignored
        // TODO: Black generation /UCR and /BG entries ignored
        // TODO: Halftone (/HT) ignored
        // TODO: Flatness (/FL) ignored

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

            if (LineWidth.HasValue)
            {
                graphicsState.LineWidth = LineWidth.Value;
            }
            if (LineCap.HasValue)
            {
                graphicsState.LineCap = LineCap.Value;
            }
            if (LineJoin.HasValue)
            {
                graphicsState.LineJoin = LineJoin.Value;
            }
            if (MiterLimit.HasValue)
            {
                graphicsState.MiterLimit = MiterLimit.Value;
            }
            if (DashPattern != null)
            {
                graphicsState.DashPattern = DashPattern;
                graphicsState.DashPhase = DashPhase.GetValueOrDefault();
            }
            if (StrokeAlpha.HasValue)
            {
                graphicsState.StrokeAlpha = StrokeAlpha.Value;
            }
            if (FillAlpha.HasValue)
            {
                graphicsState.FillAlpha = FillAlpha.Value;
            }
            if (BlendMode.HasValue)
            {
                graphicsState.BlendMode = BlendMode.Value;
            }
            if (SoftMask != null || (SoftMask == null && graphicsState.SoftMask != null))
            {
                graphicsState.SoftMask = SoftMask;
            }
            if (Knockout.HasValue)
            {
                graphicsState.Knockout = Knockout.Value;
            }
            if (OverprintMode.HasValue)
            {
                graphicsState.OverprintMode = OverprintMode.Value;
            }
            if (OverprintStroke.HasValue)
            {
                graphicsState.OverprintStroke = OverprintStroke.Value;
            }
            if (OverprintFill.HasValue)
            {
                graphicsState.OverprintFill = OverprintFill.Value;
            }
            if (TransformMatrix.HasValue)
            {
                var matrix = TransformMatrix.Value;
                graphicsState.CTM = matrix.PostConcat(graphicsState.CTM);
            }
        }
    }
}
