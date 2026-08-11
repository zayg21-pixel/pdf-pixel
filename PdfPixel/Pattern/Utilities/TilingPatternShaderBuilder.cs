using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Pattern.Model;
using PdfPixel.Forms;
using PdfPixel.Rendering.State;
using System.Collections.Generic;

namespace PdfPixel.Pattern.Utilities;

/// <summary>
/// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/> for deferred replay.
/// </summary>
internal sealed class TilingPatternShaderBuilder
{
    /// <summary>
    /// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/>.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="sourceState">Source state for rendering.</param>
    /// <returns>A <see cref="PdfCommandRecorder"/> containing the recorded pattern cell, or null if the cell is empty.</returns>
    public static PdfCommandRecorder? RenderTilingCell(IPdfRenderer renderer, PdfTilingPattern pattern, PdfGraphicsState sourceState)
    {
        PdfReference patternReference = pattern.SourceObject.Reference;
        Dictionary<PdfReference, PdfCommandRecorder> cellCache = sourceState.Page.Document.ObjectCache.TilingCells;

        if (patternReference.IsValid && cellCache.TryGetValue(patternReference, out PdfCommandRecorder? cachedCell))
        {
            return cachedCell;
        }

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

        // Anything already under way is suppressed wherever the cell reaches it again, which makes
        // the recording specific to this use; only a cell reached with nothing else in flight holds
        // for every other use of the pattern.
        bool reachedAtTopLevel = sourceState.RecursionGuard.Count == 0;

        sourceState.RecursionGuard.Add(pattern.SourceObject.Reference.ObjectNumber);

        PdfCommandRecorder recorder = new();

        // Render pattern cell without tint or color filter
        FormXObjectPageWrapper patternPage = new(pattern.SourceObject);
        PdfGraphicsState cellState = new(patternPage, sourceState);
        PdfContentStreamRenderer contentRenderer = new(renderer, patternPage);
        PdfParseContext parseContext = new(streamData);
        contentRenderer.RenderContext(recorder, ref parseContext, cellState);

        sourceState.RecursionGuard.Remove(pattern.SourceObject.Reference.ObjectNumber);

        if (recorder.Commands.Count == 0)
        {
            return null;
        }

        if (patternReference.IsValid && reachedAtTopLevel)
        {
            cellCache[patternReference] = recorder;
        }

        return recorder;
    }
}
