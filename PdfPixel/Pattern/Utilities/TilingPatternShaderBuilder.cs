using SkiaSharp;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Pattern.Model;
using PdfPixel.Forms;
using PdfPixel.Rendering.State;

namespace PdfPixel.Pattern.Utilities;

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
    /// <param name="sourceState">Source state for rendering.</param>
    /// <returns><see cref="SKPicture"/> Containing the rendered pattern cell.</returns>
    public static SKPicture RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern, PdfGraphicsState sourceState)
    {
        var streamData = pattern.SourceObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return null;
        }

        if (sourceState.RecursionGuard.Contains(pattern.SourceObject.Reference.ObjectNumber))
        {
            // Prevent infinite recursion.
            return null;
        }

        sourceState.RecursionGuard.Add(pattern.SourceObject.Reference.ObjectNumber);

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(pattern.BBox);

        // Render pattern cell without tint or color filter
        var patternPage = new FormXObjectPageWrapper(pattern.SourceObject);
        var cellState = new PdfGraphicsState(patternPage, sourceState);
        var contentRenderer = new PdfContentStreamRenderer(renderer, patternPage);
        var parseContext = new PdfParseContext(streamData);
        contentRenderer.RenderContext(canvas, ref parseContext, cellState);

        sourceState.RecursionGuard.Remove(pattern.SourceObject.Reference.ObjectNumber);

        return recorder.EndRecording();
    }
}
