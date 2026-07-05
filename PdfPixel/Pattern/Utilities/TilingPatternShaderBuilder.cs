using Microsoft.Extensions.Logging;
using SkiaSharp;
using PdfPixel.Commands;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Pattern.Model;
using PdfPixel.Forms;
using PdfPixel.Rendering.State;

namespace PdfPixel.Pattern.Utilities;

/// <summary>
/// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/> for deferred replay.
/// </summary>
internal sealed class TilingPatternShaderBuilder
{
    /// <summary>
    /// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/>.
    /// Does not apply tint or color filter; the caller applies an <see cref="IPdfCommandModifier"/>
    /// during replay for uncolored patterns.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="sourceState">Source state for rendering.</param>
    /// <returns>A <see cref="PdfCommandRecorder"/> containing the recorded pattern cell, or null if the cell is empty.</returns>
    public static PdfCommandRecorder? RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern, PdfGraphicsState sourceState)
    {
        System.ReadOnlyMemory<byte> streamData = pattern.SourceObject.DecodeAsMemory();

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

        PdfCommandRecorder recorder = new(sourceState.Page.Document.LoggerFactory.CreateLogger<PdfCommandRecorder>());

        // Clip to pattern cell bounds
        recorder.Process(new ClipRectangleCommand(pattern.BBox, SKClipOperation.Intersect));

        // Render pattern cell without tint or color filter
        FormXObjectPageWrapper patternPage = new(pattern.SourceObject);
        PdfGraphicsState cellState = new(patternPage, sourceState);
        PdfContentStreamRenderer contentRenderer = new(renderer, patternPage);
        PdfParseContext parseContext = new(streamData);
        contentRenderer.RenderContext(recorder, ref parseContext, cellState);

        sourceState.RecursionGuard.Remove(pattern.SourceObject.Reference.ObjectNumber);

        if (recorder.Commands.Count == 0)
        {
            recorder.Dispose();
            return null;
        }

        return recorder;
    }
}
