using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Arithmetic symbol dictionary decoder (ITU-T T.88 Section 6.5).
/// All state is supplied via <see cref="Jbig2SymbolArithmeticContext"/>; this class is
/// stateless and every method is static.
/// </summary>
internal static class Jbig2SymbolDictionaryArithmeticDecoder
{
    /// <summary>
    /// Decodes all new symbols from a coded bitstream and applies the export procedure
    /// (ITU-T T.88 Section 6.5.10), returning only the exported symbols.
    /// </summary>
    /// <param name="codedData">Raw arithmetic-coded bytes, starting at <see cref="Jbig2SymbolDictionarySegmentInfo.DataOffset"/>.</param>
    /// <param name="context">Probability context arrays for this decode session.</param>
    /// <param name="referredSymbols">Symbols imported from referred-to segments.</param>
    /// <returns>
    /// Exported symbols (referred + new, filtered by the IAEX export flags), or all new symbols
    /// when the export procedure fails to produce any output.
    /// </returns>
    internal static Jbig2Bitmap[] Decode(
        ReadOnlySpan<byte> codedData,
        Jbig2SymbolArithmeticContext context,
        List<Jbig2Bitmap> referredSymbols)
    {
        Jbig2SymbolDictionarySegmentInfo info = context.SegmentInfo;
        int newSymbolCount = info.NewSymbolCount;
        int templateId = info.Flags.Template;
        var atPixels = info.AtPixels ?? new Jbig2AtPixels(Array.Empty<sbyte>(), Array.Empty<sbyte>());

        var decoder = new Jbig2ArithmeticReader(codedData);
        var newSymbols = new List<Jbig2Bitmap>(newSymbolCount);
        int heightOffset = 0;

        // 6.5.6 – Decode height classes
        while (newSymbols.Count < newSymbolCount)
        {
            if (!decoder.DecodeInteger(context.HeightContexts, out int deltaHeight))
            {
                break;
            }

            heightOffset += deltaHeight;

            int symbolWidth = 0;

            // 6.5.7 – Decode symbol widths and bitmaps within this height class
            while (true)
            {
                if (!decoder.DecodeInteger(context.WidthContexts, out int deltaWidth))
                {
                    // OOB signals end of height class
                    break;
                }

                symbolWidth += deltaWidth;

                if (symbolWidth <= 0 || heightOffset <= 0)
                {
                    newSymbols.Add(Jbig2Bitmap.Empty);
                    continue;
                }

                Jbig2Bitmap symbolBitmap;
                if (info.Flags.UseRefinementAggregation)
                {
                    symbolBitmap = DecodeAggregateSymbol(
                        ref decoder,
                        context,
                        symbolWidth,
                        heightOffset,
                        referredSymbols,
                        newSymbols);
                }
                else
                {
                    // 6.5.8.1 – Direct-coded symbol bitmap
                    symbolBitmap = DecodeSymbolBitmap(
                        ref decoder,
                        context.GenericContexts.AsSpan(),
                        symbolWidth,
                        heightOffset,
                        templateId,
                        atPixels);
                }

                newSymbols.Add(symbolBitmap);
            }
        }
        if (newSymbols.Count < newSymbolCount)
        {
            throw new InvalidOperationException(
                $"JBIG2 symbol dictionary: decoded {newSymbols.Count} symbols but expected {newSymbolCount}.");
        }

        // 6.5.10 – Export symbols
        var exported = ExportSymbols(ref decoder, context.ExportContexts, referredSymbols, newSymbols);
        // Fall back to all new symbols if the export procedure produced nothing
        if (exported.Length == 0 && newSymbols.Count > 0)
        {
            return newSymbols.ToArray();
        }

        return exported;
    }

    /// <summary>
    /// Decodes a refinement/aggregate-coded symbol bitmap (ITU-T T.88 Section 6.5.8.2).
    /// Delegates to the inline text-region decoder for multi-instance aggregates, or to
    /// the refinement region decoder for single-instance refinement.
    /// Tracks inline depth via <see cref="Jbig2SymbolArithmeticContext.InlineLevel"/> and
    /// bails out with a blank bitmap when <see cref="Jbig2SymbolArithmeticContext.InlineLimit"/> is exceeded.
    /// </summary>
    private static Jbig2Bitmap DecodeAggregateSymbol(
        ref Jbig2ArithmeticReader decoder,
        Jbig2SymbolArithmeticContext context,
        int symbolWidth,
        int symbolHeight,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2Bitmap> newSymbols)
    {
        decoder.DecodeInteger(context.IaaiContexts, out int numberOfInstances);

        var ac = context.AggregateContext;
        ac.InlineLevel++;
        if (ac.InlineLevel > Jbig2ArithmeticContext.InlineLimit)
        {
            ac.InlineLevel--;
            return new Jbig2Bitmap(symbolWidth > 0 ? symbolWidth : 1, symbolHeight > 0 ? symbolHeight : 1);
        }

        Jbig2Bitmap result;
        if (numberOfInstances > 1)
        {
            // Multi-instance aggregate: treat as an inline text region (Section 6.5.8.2)
            var combinedSymbols = new List<Jbig2Bitmap>(referredSymbols.Count + newSymbols.Count);
            combinedSymbols.AddRange(referredSymbols);
            combinedSymbols.AddRange(newSymbols);

            var placements = Jbig2TextRegionDecoder.DecodeTextRegionInline(
                ref decoder,
                symbolWidth,
                symbolHeight,
                numberOfInstances,
                combinedSymbols,
                context.AggregateContext);

            result = new Jbig2Bitmap(symbolWidth, symbolHeight);
            placements.Compose(result, 0, 0, Jbig2CombinationOperator.Replace);
        }
        else
        {
            // Single-instance refinement
            int symbolId = decoder.DecodeIaid(context.AggregateContext.IaId, context.SymbolCodeLength);
            decoder.DecodeInteger(context.AggregateContext.Iardx, out int rdx);
            decoder.DecodeInteger(context.AggregateContext.Iardy, out int rdy);

            Jbig2Bitmap refSymbol;
            if (symbolId < referredSymbols.Count)
            {
                refSymbol = referredSymbols[symbolId];
            }
            else
            {
                int newIndex = symbolId - referredSymbols.Count;
                refSymbol = newIndex < newSymbols.Count
                    ? newSymbols[newIndex]
                    : Jbig2Bitmap.Empty;
            }

            result = Jbig2RefinementRegionDecoder.DecodeInline(
                ref decoder,
                context.AggregateContext,
                symbolWidth,
                symbolHeight,
                refSymbol,
                rdx,
                rdy);
        }

        ac.InlineLevel--;
        return result;
    }

    /// <summary>
    /// Decodes a direct-coded symbol bitmap using generic region decoding (ITU-T T.88 Section 6.5.8.1).
    /// </summary>
    private static Jbig2Bitmap DecodeSymbolBitmap(
        ref Jbig2ArithmeticReader decoder,
        Span<byte> contexts,
        int width,
        int height,
        int templateId,
        Jbig2AtPixels atPixels)
    {
        var bitmap = new Jbig2Bitmap(width, height);
        var fastTemplate = Jbig2Templates.BuildFastTemplate(templateId, atPixels);

        for (int y = 0; y < height; y++)
        {
            Jbig2RowDecoder.DecodeRow(ref decoder, bitmap, contexts, y, width, fastTemplate);
        }

        return bitmap;
    }

    /// <summary>
    /// Decodes the IAEX export-flag runs and returns only the flagged symbols
    /// (ITU-T T.88 Section 6.5.10).
    /// </summary>
    private static Jbig2Bitmap[] ExportSymbols(
        ref Jbig2ArithmeticReader decoder,
        byte[] exportContexts,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2Bitmap> newSymbols)
    {
        int totalSymbols = referredSymbols.Count + newSymbols.Count;
        var exportFlags = new bool[totalSymbols];
        bool currentFlag = false;
        int flagIndex = 0;

        while (flagIndex < totalSymbols)
        {
            if (!decoder.DecodeInteger(exportContexts, out int runLength))
            {
                break;
            }

            for (int r = 0; r < runLength && flagIndex < totalSymbols; r++)
            {
                exportFlags[flagIndex] = currentFlag;
                flagIndex++;
            }

            currentFlag = !currentFlag;
        }

        var exported = new List<Jbig2Bitmap>();
        for (int i = 0; i < referredSymbols.Count; i++)
        {
            if (i < exportFlags.Length && exportFlags[i])
            {
                exported.Add(referredSymbols[i]);
            }
        }

        for (int j = 0; j < newSymbols.Count; j++)
        {
            int idx = referredSymbols.Count + j;
            if (idx < exportFlags.Length && exportFlags[idx])
            {
                exported.Add(newSymbols[j]);
            }
        }

        return exported.ToArray();
    }
}
