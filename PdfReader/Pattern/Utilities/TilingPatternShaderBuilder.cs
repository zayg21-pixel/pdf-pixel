using PdfReader.Color.Paint;
using PdfReader.Forms;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Pattern.Model;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Pattern.Utilities;

/// <summary>
/// Converts PDF tiling and shading patterns into SkiaSharp <see cref="SKShader"/> instances for rendering.
/// Ensures patterns are anchored, transformed, and optimized for device-space rendering and performance.
/// </summary>
internal sealed class TilingPatternShaderBuilder
{
    /// <summary>
    /// Creates a base <see cref="SKPicture"/> for the given PDF tiling pattern and page context.
    /// Does not apply CTM or color filter/tint for uncolored patterns.
    /// The returned instance is suitable for further transformation and color filtering.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">The PDF tiling pattern to convert.</param>
    /// <param name="page">Current page context.</param>
    /// <returns>Base <see cref="SKPicture"/> instance or null if creation fails.</returns>
    public static SKPicture ToBaseShader(IPdfRenderer renderer, PdfTilingPattern pattern, PdfPage page)
    {
        if (pattern == null)
        {
            return null;
        }

        var bbox = pattern.BBox;
        if (bbox.Width <= 0f || bbox.Height <= 0f)
        {
            return null;
        }

        SKPicture cellPicture = RenderTilingCell(renderer, pattern, page);

        if (cellPicture == null)
        {
            return null;
        }

        using var fillPicture = new SKPictureRecorder();
        var unrestrictedBounds = new SKRect(float.NegativeInfinity, float.NegativeInfinity, float.PositiveInfinity, float.PositiveInfinity);

        using var fullCanvas = fillPicture.BeginRecording(unrestrictedBounds);

        using var shader = SKShader.CreatePicture(
            cellPicture,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKFilterMode.Linear);

        using var basePaint = PdfPaintFactory.CreateShaderPaint(antiAlias: true);
        basePaint.Shader = shader;

        fullCanvas.DrawPaint(basePaint);

        return fillPicture.EndRecording();
    }

    /// <summary>
    /// Renders a single tiling pattern cell to an <see cref="SKPicture"/>. Does not apply tint or color filter.
    /// The returned picture is suitable for use with <see cref="SKShader"/> and post-factum color filtering.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="page">Current page context.</param>
    /// <returns><see cref="SKPicture"/> containing the rendered pattern cell.</returns>
    private static SKPicture RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern, PdfPage page)
    {
        var streamData = pattern.SourceObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return null;
        }

        var cellState = new PdfGraphicsState();
        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(pattern.BBox);

        // Render pattern cell without tint or color filter
        var recursionGuard = new HashSet<int>();
        var patternPage = new FormXObjectPageWrapper(page, pattern.SourceObject);
        var contentRenderer = new PdfContentStreamRenderer(renderer, patternPage);
        var parseContext = new PdfParseContext(streamData);
        contentRenderer.RenderContext(canvas, ref parseContext, cellState, recursionGuard);

        return recorder.EndRecording();
    }
}
