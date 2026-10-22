using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Commands;
using PdfPixel.Objects;
using PdfPixel.Resources;

namespace PdfPixel.Pattern;

/// <summary>
/// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/> for deferred replay.
/// </summary>
internal sealed class TilingPatternCellRenderer
{
    /// <summary>
    /// Renders a single tiling pattern cell into a <see cref="PdfCommandRecorder"/>.
    /// </summary>
    /// <param name="pattern">Tiling pattern definition.</param>
    /// <param name="sourceState">State supplying the recursion guard, observer and rendering parameters.</param>
    /// <returns>A <see cref="PdfCommandRecorder"/> containing the recorded pattern cell, or null if the cell is empty.</returns>
    public static PdfCommandRecorder? RenderTilingCell(PdfTilingPattern pattern, PdfGraphicsState sourceState)
    {
        PdfReference patternReference = pattern.SourceReference;

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

        sourceState.RecursionGuard.Add(patternReference.ObjectNumber);

        PdfCommandRecorder recorder = new();

        // Render pattern cell without tint or color filter
        IPdfDocumentInternal document = sourceState.Host.Document;
        PdfResourceNode cellResources = (pattern.Resources == null) ? PdfResourceNode.CreateEmpty(document) : new PdfResourceNode(document, new PdfResourceDictionary(pattern.Resources), null);
        PdfContentHost patternHost = new(document, cellResources, null);
        PdfGraphicsState cellState = new(patternHost, pattern.BoundingBox, sourceState);
        PdfRenderer cellRenderer = new(document.LoggerFactory);
        PdfContentStreamRenderer contentRenderer = new(cellRenderer, patternHost);
        PdfParseContext parseContext = new(streamData, document);
        contentRenderer.RenderContext(recorder, ref parseContext, cellState);

        sourceState.RecursionGuard.Remove(patternReference.ObjectNumber);

        if (recorder.Commands.Count == 0)
        {
            return null;
        }

        return recorder;
    }
}
