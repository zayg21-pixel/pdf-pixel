using System;
using System.Collections.Generic;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Rendering.Color;
using PdfReader.Rendering.Advanced;
using PdfReader.Streams;
using PdfReader.Parsing;

namespace PdfReader.Rendering.Pattern
{
    internal static class PatternPaintEngine
    {
        public static bool TryRenderPattern(
            SKCanvas canvas,
            IPatternPaintTarget target,
            PdfGraphicsState state,
            PdfPage page,
            PdfPaint paint,
            PdfColorSpaceConverter colorConverter)
        {
            if (paint == null || !paint.IsPattern)
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

        private static void RenderTilingPattern(
            SKCanvas canvas,
            PdfTilingPattern pattern,
            PdfGraphicsState state,
            PdfPage page,
            IPatternPaintTarget target,
            PdfPaint originalPaint,
            PdfColorSpaceConverter colorConverter)
        {
            if (pattern.StreamObject == null || pattern.StreamObject.StreamData.IsEmpty)
            {
                return;
            }

            var bbox = pattern.BBox;
            if (bbox.Width <= 0f || bbox.Height <= 0f || pattern.XStep == 0f || pattern.YStep == 0f)
            {
                return;
            }

            var data = PdfStreamDecoder.DecodeContentStream(pattern.StreamObject);
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
                    var patternPage = new FormXObjectPageWrapper(page, pattern.StreamObject);
                    var renderer = new PdfContentStreamRenderer(page);
                    renderer.RenderContext(canvas, ref parseContext, cellState, recursionGuard);

                    canvas.Restore();
                }
            }

            canvas.Restore(); // Clip
        }

        private static void RenderShadingPattern(SKCanvas canvas, PdfShadingPattern shadingPattern, PdfGraphicsState state, PdfPage page, IPatternPaintTarget target)
        {
            if (shadingPattern.ShadingDictionary == null)
            {
                return;
            }

            canvas.Save();
            target.ApplyClip(canvas); // Clip established under dynamic CTM.

            // Bounds currently in device space; used for soft mask / optimization.
            var deviceBounds = target.GetDeviceBounds(canvas);
            if (deviceBounds.IsEmpty)
            {
                deviceBounds = canvas.LocalClipBounds;
            }

            // Same anchoring rationale as tiling patterns (PDF 2.0 8.7.3 Shading Patterns).
            SKMatrix ctmInverse = state.CTM.Invert();

            using var softMaskScope = new SoftMaskDrawingScope(canvas, state, page, deviceBounds);
            softMaskScope.BeginDrawContent();

            canvas.Concat(ctmInverse); // Remove dynamic CTM so pattern anchors to default user space.
            canvas.Concat(shadingPattern.PatternMatrix); // Enter pattern space anchored to base user space.

            page.Document.PdfRenderer.ShadingDrawer.DrawShading(canvas, shadingPattern.ShadingDictionary, state, page);

            softMaskScope.EndDrawContent();

            canvas.Restore();
        }
    }
}
