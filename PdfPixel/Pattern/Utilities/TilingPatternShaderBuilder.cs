using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Models;
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
    /// </summary>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="sourceState">Source state for rendering.</param>
    /// <returns>A <see cref="PdfCommandRecorder"/> containing the recorded pattern cell, or null if the cell is empty.</returns>
    public static PdfCommandRecorder? RenderTilingCell(PdfTilingPattern pattern, PdfGraphicsState sourceState)
    {
        PdfReference patternReference = pattern.SourceReference;

        if (pattern.CellRecording != null)
        {
            return pattern.CellRecording;
        }

        System.ReadOnlyMemory<byte> streamData = pattern.SourceStream.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return null;
        }

        if (sourceState.RecursionGuard.Contains(patternReference.ObjectNumber))
        {
            // Prevent infinite recursion.
            return null;
        }

        // Anything already under way is suppressed wherever the cell reaches it again, which makes
        // the recording specific to this use; only a cell reached with nothing else in flight holds
        // for every other use of the pattern.
        bool reachedAtTopLevel = sourceState.RecursionGuard.Count == 0;

        sourceState.RecursionGuard.Add(patternReference.ObjectNumber);

        PdfCommandRecorder recorder = new();

        // Render pattern cell without tint or color filter
        FormXObjectPageWrapper patternPage = new(
            sourceState.Page.Document,
            patternReference,
            pattern.SourceStream,
            pattern.CellResources);
        PdfGraphicsState cellState = new(patternPage, sourceState);
        PdfRenderer cellRenderer = new(sourceState.Page.Document.LoggerFactory);
        PdfContentStreamRenderer contentRenderer = new(cellRenderer, patternPage);
        PdfParseContext parseContext = new(streamData);
        contentRenderer.RenderContext(recorder, ref parseContext, cellState);

        sourceState.RecursionGuard.Remove(patternReference.ObjectNumber);

        if (recorder.Commands.Count == 0)
        {
            return null;
        }

        if (patternReference.IsValid && reachedAtTopLevel)
        {
            pattern.CellRecording = recorder;
        }

        return recorder;
    }
}
