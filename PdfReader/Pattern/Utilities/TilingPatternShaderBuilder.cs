using System.Collections.Generic;
using SkiaSharp;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Pattern.Model;
using PdfReader.Forms;
using PdfReader.Rendering.State;

namespace PdfReader.Pattern.Utilities;

/// <summary>
/// Converts PDF tiling and shading patterns into SkiaSharp <see cref="SKShader"/> instances for rendering.
/// Ensures patterns are anchored, transformed, and optimized for device-space rendering and performance.
/// </summary>
internal sealed class TilingPatternShaderBuilder
{
    /// <summary>
    /// Creates a base <see cref="SKShader"/> for the given PDF tiling pattern and page context.
    /// Does not apply CTM or color filter/tint for uncolored patterns.
    /// The returned shader is suitable for further transformation and color filtering.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">The PDF tiling pattern to convert.</param>
    /// <returns>Base <see cref="SKShader"/> instance or null if creation fails.</returns>
    public static SKShader ToBaseShader(IPdfRenderer renderer, PdfTilingPattern pattern)
    {
        if (pattern == null)
        {
            return null;
        }

        SKPicture cellPicture = RenderTilingCell(renderer, pattern);
        if (cellPicture == null)
        {
            return null;
        }

        return SKShader.CreatePicture(
            cellPicture,
            SKShaderTileMode.Repeat,
            SKShaderTileMode.Repeat,
            SKFilterMode.Nearest,
            SKMatrix.Identity,
            pattern.BBox);
    }

    /// <summary>
    /// Renders a single tiling pattern cell to an <see cref="SKPicture"/>. Does not apply tint or color filter.
    /// The returned picture is suitable for use with <see cref="SKShader"/> and post-factum color filtering.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <returns><see cref="SKPicture"/> Containing the rendered pattern cell.</returns>
    public static SKPicture RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern)
    {
        var streamData = pattern.SourceObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return null;
        }

        var cellState = new PdfGraphicsState();
        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(pattern.BBox);
        canvas.ClipRect(pattern.BBox);

        // Render pattern cell without tint or color filter
        var recursionGuard = new HashSet<int>();
        var patternPage = new FormXObjectPageWrapper(pattern.SourceObject);
        var contentRenderer = new PdfContentStreamRenderer(renderer, patternPage);
        var parseContext = new PdfParseContext(streamData);
        contentRenderer.RenderContext(canvas, ref parseContext, cellState, recursionGuard);

        return recorder.EndRecording();
    }
}
