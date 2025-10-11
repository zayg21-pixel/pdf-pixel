using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;
using PdfReader.Streams;
using PdfReader.Parsing;

namespace PdfReader.Rendering.Pattern
{
    /// <summary>
    /// Engine responsible for rendering pattern paints (tiling and shading) onto a canvas.
    /// Non-static to enable future configuration (caching, diagnostics) without global state.
    /// Call <see cref="TryRenderPattern"/> from drawing code paths (fill, stroke, text, images) to apply a pattern.
    /// </summary>
    internal sealed class PatternPaintEngine
    {
        /// <summary>
        /// Attempt to render the supplied paint as a pattern onto the canvas, clipped to the target geometry.
        /// Falls back to caller drawing solid color when returns false.
        /// </summary>
        /// <param name="canvas">Destination canvas.</param>
        /// <param name="target">Geometry abstraction providing clip and device bounds.</param>
        /// <param name="state">Current graphics state (provides CTM, converters, etc.).</param>
        /// <param name="page">Current page context.</param>
        /// <param name="paint">Paint to render (pattern or solid).</param>
        /// <param name="colorConverter">Color space converter associated with the paint (for uncolored patterns).</param>
        /// <returns>True if a pattern was rendered; false if paint is not a pattern.</returns>
        public bool TryRenderPattern(
            SKCanvas canvas,
            IPatternPaintTarget target,
            PdfGraphicsState state,
            PdfPage page,
            PdfPaint paint,
            PdfColorSpaceConverter colorConverter)
        {
            if (paint == null)
            {
                return false;
            }

            if (!paint.IsPattern)
            {
                return false;
            }

            var pattern = paint.Pattern;
            if (pattern == null)
            {
                return false;
            }

            switch (pattern.PatternType)
            {
                case PdfPatternType.Tiling:
                    RenderTilingPattern(canvas, (PdfTilingPattern)pattern, state, page, target, paint, colorConverter);
                    return true;
                case PdfPatternType.Shading:
                    RenderShadingPattern(canvas, (PdfShadingPattern)pattern, state, page, target);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Render a tiling pattern within the geometry clip provided by the target.
        /// </summary>
        private static void RenderTilingPattern(
            SKCanvas canvas,
            PdfTilingPattern pattern,
            PdfGraphicsState state,
            PdfPage page,
            IPatternPaintTarget target,
            PdfPaint originalPaint, PdfColorSpaceConverter colorConverter)
        {
            var streamObject = page.Document.GetObject(pattern.Reference);

            var bbox = pattern.BBox;
            if (bbox.Width <= 0f || bbox.Height <= 0f || pattern.XStep == 0f || pattern.YStep == 0f)
            {
                return;
            }

            var data = PdfStreamDecoder.DecodeContentStream(streamObject);
            if (data.IsEmpty)
            {
                return;
            }

            // Placeholder phase (anchoring). PDF spec allows implementation-defined anchoring; keep (0,0) for now.
            float phaseX = 0f;
            float phaseY = 0f;

            canvas.Save();
            target.ApplyClip(canvas); // Clip is established in device space with current CTM applied to geometry.

            // Device-space bounds of the geometry being painted (conservative) used to restrict tiling iteration.
            var deviceBounds = target.GetDeviceBounds(canvas);

            // ------------------------------------------------------------------------------------------
            // PDF 2.0 (8.7 Patterns): A pattern's /Matrix maps pattern space to the *default user space* of
            // the context (page or form) – NOT the dynamic CTM in effect when the painting operator executes.
            // The dynamic CTM affects only the geometry (thus the clip), acting like a moving window over a
            // wallpaper anchored to default user space. Therefore remove the current CTM when entering pattern
            // space so the pattern does not "follow" subsequent cm operations.
            // We derive base user space as: BaseUser = canvas.TotalMatrix * inverse(state.CTM)
            // (assuming TotalMatrix currently equals BaseUser * state.CTM).
            // CombinedPatternTransform (pattern -> device) = BaseUser * PatternMatrix.
            // ------------------------------------------------------------------------------------------
            SKMatrix baseUserSpace;
            SKMatrix ctmInverse;
            bool haveCtmInverse = state.CTM.TryInvert(out ctmInverse);
            if (haveCtmInverse)
            {
                baseUserSpace = SKMatrix.Concat(canvas.TotalMatrix, ctmInverse); // BaseUser = Total * CTM^{-1}
            }
            else
            {
                // Degenerate CTM (unlikely); fall back to current total matrix so we at least draw something.
                baseUserSpace = canvas.TotalMatrix;
            }

            // device = (baseUserSpace * pattern.PatternMatrix) * patternCoord
            var combined = SKMatrix.Concat(baseUserSpace, pattern.PatternMatrix);

            // Determine which pattern cells intersect the device bounds by mapping device->pattern space.
            SKRect patternSpaceBounds;
            if (combined.TryInvert(out var combinedInverse))
            {
                patternSpaceBounds = combinedInverse.MapRect(deviceBounds);
            }
            else
            {
                // Fallback: if non-invertible, just use device bounds (overdraw acceptable).
                patternSpaceBounds = deviceBounds;
            }

            // Calculate tiling indices covering the visible pattern-space bounds (with one extra tile for seams).
            float minPatX = patternSpaceBounds.Left - pattern.XStep;
            float maxPatX = patternSpaceBounds.Right + pattern.XStep;
            float minPatY = patternSpaceBounds.Top - pattern.YStep;
            float maxPatY = patternSpaceBounds.Bottom + pattern.YStep;

            int iMin = (int)Math.Floor((minPatX - phaseX) / pattern.XStep);
            int iMax = (int)Math.Ceiling((maxPatX - phaseX) / pattern.XStep);
            int jMin = (int)Math.Floor((minPatY - phaseY) / pattern.YStep);
            int jMax = (int)Math.Ceiling((maxPatY - phaseY) / pattern.YStep);

            // Prepare graphics state for pattern cell content stream (color resolution for uncolored patterns).
            var cellState = state.Clone();
            cellState.FillPaint = PdfPaint.Solid(originalPaint.Color);
            cellState.StrokePaint = PdfPaint.Solid(originalPaint.Color);

            // Resolve tint for uncolored (stencil) patterns using the appropriate color space converter from state.
            if (pattern.PaintTypeKind == PdfTilingPaintType.Uncolored && originalPaint.PatternComponents != null)
            {
                if (colorConverter is PatternColorSpaceConverter pcs && pcs.BaseColorSpace != null)
                {
                    var tinted = pcs.BaseColorSpace.ToSrgb(originalPaint.PatternComponents, cellState.RenderingIntent);
                    cellState.FillPaint = PdfPaint.Solid(tinted);
                    cellState.StrokePaint = PdfPaint.Solid(tinted);
                }
            }

            var recursionGuard = new HashSet<int>();
            const float SeamExpansion = 0.25f; // Slight expansion reduces visible seams between tiles.
            var expanded = new SKRect(bbox.Left - SeamExpansion, bbox.Top - SeamExpansion, bbox.Right + SeamExpansion, bbox.Bottom + SeamExpansion);

            // Render each visible tile. For each tile:
            // 1. Remove dynamic CTM (concat CTM^{-1}) -> canvas now in base user space.
            // 2. Apply pattern matrix -> enter anchored pattern space.
            // 3. Apply tile translation in pattern space.
            // 4. Execute pattern cell content stream clipped to expanded BBox.
            var patternPage = new FormXObjectPageWrapper(page, streamObject);
            var renderer = new PdfContentStreamRenderer(patternPage);

            for (int j = jMin; j <= jMax; j++)
            {
                float tileOffsetY = phaseY + j * pattern.YStep;
                for (int i = iMin; i <= iMax; i++)
                {
                    float tileOffsetX = phaseX + i * pattern.XStep;
                    canvas.Save();

                    if (haveCtmInverse)
                    {
                        canvas.Concat(ctmInverse); // Remove dynamic CTM -> base user space.
                    }

                    canvas.Concat(pattern.PatternMatrix); // Anchoring + pattern space.
                    canvas.Translate(tileOffsetX, tileOffsetY); // Tile translation in pattern space.

                    canvas.ClipRect(expanded, antialias: true); // Clip to (slightly expanded) pattern cell bbox.

                    var parseContext = new PdfParseContext(data);
                    renderer.RenderContext(canvas, ref parseContext, cellState, recursionGuard);

                    canvas.Restore();
                }
            }

            canvas.Restore(); // Clip
        }

        /// <summary>
        /// Render a shading pattern clipped to the target geometry.
        /// </summary>
        private static void RenderShadingPattern(SKCanvas canvas, PdfShadingPattern shadingPattern, PdfGraphicsState state, PdfPage page, IPatternPaintTarget target)
        {
            if (shadingPattern.Shading == null)
            {
                return;
            }

            canvas.Save();
            target.ApplyClip(canvas); // Clip established under dynamic CTM.

            // Same anchoring rationale as tiling patterns (PDF 2.0 8.7.3 Shading Patterns).
            SKMatrix ctmInverse = state.CTM.Invert();

            canvas.Concat(ctmInverse); // Remove dynamic CTM so pattern anchors to default user space.
            canvas.Concat(shadingPattern.PatternMatrix); // Enter pattern space anchored to base user space.

            page.Document.PdfRenderer.DrawShading(canvas, shadingPattern.Shading, state, page);

            canvas.Restore();
        }
    }
}
