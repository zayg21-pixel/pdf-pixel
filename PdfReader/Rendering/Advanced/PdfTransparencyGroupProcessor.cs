using SkiaSharp;
using PdfReader.Models;

namespace PdfReader.Rendering.Advanced
{
    /// <summary>
    /// Handles PDF transparency group isolation / knockout layering.
    /// Current implementation creates an offscreen layer for isolated or knockout groups and restores afterwards.
    /// Knockout semantics are not fully implemented yet (TODO: per-object compositing pass).
    /// </summary>
    public static class PdfTransparencyGroupProcessor
    {
        /// <summary>
        /// Apply transparency group. Creates a temporary layer for isolated or knockout groups so that
        /// the group's content can be composited as a unit before blending with the backdrop.
        /// </summary>
        /// <param name="canvas">Target canvas.</param>
        /// <param name="transparencyGroup">Parsed transparency group.</param>
        /// <param name="graphicsState">Current graphics state.</param>
        /// <param name="groupBounds">Optional pre-computed bounds (typically transformed /BBox). Falls back to current clip.</param>
        public static void TryApplyTransparencyGroup(SKCanvas canvas, PdfTransparencyGroup transparencyGroup, PdfGraphicsState graphicsState, SKRect? groupBounds = null)
        {
            if (!ShouldApplyTransparencyGroup(transparencyGroup))
            {
                return;
            }

            // Only need a separate layer when isolated or knockout.
            if (!(transparencyGroup.Isolated || transparencyGroup.Knockout))
            {
                return;
            }

            var bounds = groupBounds ?? canvas.LocalClipBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                bounds = canvas.LocalClipBounds; // fallback
            }

            // Save layer without special paint; blend mode decisions for objects happen per-draw.
            // For knockout we still just isolate for now (TODO: implement per-object knockouts per PDF 11.6.6.3).
            canvas.SaveLayer(bounds, null);
        }

        /// <summary>
        /// End transparency group processing, restoring any isolation layer.
        /// </summary>
        public static void TryEndTransparencyGroup(SKCanvas canvas, PdfTransparencyGroup transparencyGroup)
        {
            if (!ShouldApplyTransparencyGroup(transparencyGroup))
            {
                return;
            }

            if (transparencyGroup.Isolated || transparencyGroup.Knockout)
            {
                canvas.Restore();
            }
        }

        /// <summary>
        /// Determines if the group requires an isolation layer.
        /// </summary>
        public static bool ShouldApplyTransparencyGroup(PdfTransparencyGroup transparencyGroup)
        {
            return transparencyGroup != null && transparencyGroup.IsTransparencyGroup && (transparencyGroup.Isolated || transparencyGroup.Knockout);
        }

        /// <summary>
        /// Creates a paint for final form compositing when additional blend/alpha needed. Currently delegates to blend mode in graphics state.
        /// </summary>
        public static SKPaint CreateTransparencyGroupPaint(PdfTransparencyGroup transparencyGroup, PdfGraphicsState graphicsState)
        {
            var paint = new SKPaint
            {
                IsAntialias = true,
            };

            // Knockout not implemented; treat same as isolated for paint selection.
            paint.BlendMode = PdfBlendModeNames.ToSkiaBlendMode(graphicsState.BlendMode);

            var alpha = (byte)(graphicsState.FillAlpha * 255f);
            paint.Color = paint.Color.WithAlpha(alpha);
            return paint;
        }
    }
}