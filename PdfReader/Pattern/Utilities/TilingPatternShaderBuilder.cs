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
    /// Renders a single tiling pattern cell to an <see cref="SKPicture"/>. Does not apply tint or color filter.
    /// The returned picture is suitable for use with <see cref="SKShader"/> and post-factum color filtering.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="page">PDF page context for rendering.</param>
    /// <returns><see cref="SKPicture"/> Containing the rendered pattern cell.</returns>
    public static SKPicture RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern, PdfPage page, HashSet<uint> recursionGuard)
    {
        var streamData = pattern.SourceObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return null;
        }

        if (recursionGuard.Contains(pattern.SourceObject.Reference.ObjectNumber))
        {
            // Prevent infinite recursion.
            return null;
        }

        recursionGuard.Add(pattern.SourceObject.Reference.ObjectNumber);

        var cellState = new PdfGraphicsState(page, recursionGuard);
        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(pattern.BBox);

        // Render pattern cell without tint or color filter
        var patternPage = new FormXObjectPageWrapper(pattern.SourceObject);
        var contentRenderer = new PdfContentStreamRenderer(renderer, patternPage);
        var parseContext = new PdfParseContext(streamData);
        contentRenderer.RenderContext(canvas, ref parseContext, cellState);

        recursionGuard.Remove(pattern.SourceObject.Reference.ObjectNumber);

        return recorder.EndRecording();
    }
}
