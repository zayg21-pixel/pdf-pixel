using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using SkiaSharp;

namespace PdfReader.Rendering.Path
{
    /// <summary>
    /// Standard path drawer supporting stroke, fill and combined operations, including pattern paints
    /// and soft mask application. Uses PathPatternPaintTarget to derive an outline path for pattern
    /// rendering in both fill and stroke scenarios.
    /// </summary>
    public class StandardPathDrawer : IPathDrawer
    {
        /// <summary>
        /// Draw a path using the specified paint operation and fill rule.
        /// Handles pattern paints, soft masks, and combined fill+stroke layering.
        /// </summary>
        public void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, PdfPage page, SKPathFillType fillType)
        {
            if (canvas == null)
            {
                return;
            }

            if (path == null || path.IsEmpty)
            {
                return;
            }

            path.FillType = fillType;

            using (var softMaskScope = new SoftMaskDrawingScope(canvas, state, page))
            {
                softMaskScope.BeginDrawContent();
                DrawPathCore(canvas, path, state, operation, page);
                softMaskScope.EndDrawContent();
            }
        }

        /// <summary>
        /// Core path drawing logic for each paint operation.
        /// SaveLayer for FillAndStroke now uses the current clip region (no explicit bounds) simplifying logic.
        /// </summary>
        private void DrawPathCore(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, PdfPage page)
        {
            switch (operation)
            {
                case PaintOperation.Stroke:
                {
                    using (var strokePaint = PdfPaintFactory.CreateStrokePaint(state, page))
                    {
                        canvas.DrawPath(path, strokePaint);
                    }
                    break;
                }
                case PaintOperation.Fill:
                {
                    using (var fillPaint = PdfPaintFactory.CreateFillPaint(state, page))
                    {
                        canvas.DrawPath(path, fillPaint);
                    }
                    break;
                }
                case PaintOperation.FillAndStroke:
                {
                    // Fill phase.
                    using (var fillPaint = PdfPaintFactory.CreateFillPaint(state, page))
                    {
                        canvas.DrawPath(path, fillPaint);
                    }

                    // Stroke phase.
                    using (var strokePaint = PdfPaintFactory.CreateStrokePaint(state, page))
                    {
                        strokePaint.BlendMode = SKBlendMode.Src;
                        canvas.DrawPath(path, strokePaint);
                    }
                    break;
                }
            }
        }
    }
}