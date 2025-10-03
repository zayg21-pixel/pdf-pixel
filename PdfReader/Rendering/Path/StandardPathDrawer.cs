using PdfReader.Models;
using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.Pattern;
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
            if (path == null || path.IsEmpty)
            {
                return;
            }

            path.FillType = fillType;

            var layerBounds = ComputePathLayerBounds(canvas, path, state, operation);
            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page, layerBounds);
            softMaskScope.BeginDrawContent();

            DrawPathCore(canvas, path, state, operation, page, layerBounds);

            softMaskScope.EndDrawContent();
        }

        private static SKRect ComputePathLayerBounds(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation)
        {
            var bounds = path.Bounds;
            if (operation == PaintOperation.Stroke || operation == PaintOperation.FillAndStroke)
            {
                // Inflate to cover stroke thickness (approximate) plus a small cushion for joins/caps.
                var inflate = System.Math.Max(1f, (state.LineWidth * 0.5f) + 1f);
                bounds.Inflate(inflate, inflate);
            }

            var clip = canvas.LocalClipBounds;
            var tight = SKRect.Intersect(clip, bounds);
            return tight.IsEmpty ? clip : tight;
        }

        private static void DrawPathCore(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, PdfPage page, SKRect bounds)
        {
            switch (operation)
            {
                case PaintOperation.Stroke:
                {
                    using (var strokePaint = PdfPaintFactory.CreateStrokePaint(state, page))
                    {
                        var strokeTarget = new PathPatternPaintTarget(path, strokePaint);
                        if (!PatternPaintEngine.TryRenderPattern(canvas, strokeTarget, state, page, state.StrokePaint, state.StrokeColorConverter))
                        {
                            canvas.DrawPath(path, strokePaint);
                        }
                    }
                    break;
                }
                case PaintOperation.Fill:
                {
                    using (var fillPaint = PdfPaintFactory.CreateFillPaint(state, page))
                    {
                        var fillTarget = new PathPatternPaintTarget(path, fillPaint);
                        if (!PatternPaintEngine.TryRenderPattern(canvas, fillTarget, state, page, state.FillPaint, state.FillColorConverter))
                        {
                            canvas.DrawPath(path, fillPaint);
                        }
                    }
                    break;
                }
                case PaintOperation.FillAndStroke:
                {
                    using (var layerPaint = new SKPaint
                    {
                        IsAntialias = true,
                        BlendMode = PdfBlendModeNames.ToSkiaBlendMode(state.BlendMode)
                    })
                    {
                        canvas.SaveLayer(bounds, layerPaint);
                        try
                        {
                            // Fill phase
                            using (var fillPaint = PdfPaintFactory.CreateFillPaint(state, page))
                            {
                                var fillTarget = new PathPatternPaintTarget(path, fillPaint);
                                if (!PatternPaintEngine.TryRenderPattern(canvas, fillTarget, state, page, state.FillPaint, state.FillColorConverter))
                                {
                                    canvas.DrawPath(path, fillPaint);
                                }
                            }

                            // Stroke phase
                            using (var strokePaint = PdfPaintFactory.CreateStrokePaint(state, page))
                            {
                                // When layering stroke atop fill within a save layer we want the stroke to replace pixels where it lies.
                                strokePaint.BlendMode = SKBlendMode.SrcOver;
                                var strokeTarget = new PathPatternPaintTarget(path, strokePaint);
                                if (!PatternPaintEngine.TryRenderPattern(canvas, strokeTarget, state, page, state.StrokePaint, state.StrokeColorConverter))
                                {
                                    canvas.DrawPath(path, strokePaint);
                                }
                            }
                        }
                        finally
                        {
                            canvas.Restore();
                        }
                    }
                    break;
                }
            }
        }
    }
}