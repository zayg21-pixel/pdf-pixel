using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes JBIG2 symbol dictionary segments (ITU-T T.88 Section 6.5).
/// A symbol dictionary produces a collection of symbol bitmaps that can be
/// referenced by subsequent text region segments.
/// </summary>
internal static class Jbig2SymbolDictionaryDecoder
{
    /// <summary>
    /// Decodes a symbol dictionary segment and returns the array of decoded symbol bitmaps.
    /// </summary>
    /// <param name="segmentData">Encoded segment data (after the segment header).</param>
    /// <param name="referredSymbols">Symbols from referred-to segments (imported symbols).</param>
    /// <param name="customTables">User-defined Huffman tables from referred Table segments.</param>
    /// <returns>Array of decoded symbol bitmaps.</returns>
    public static Jbig2Bitmap[] Decode(
        in ReadOnlySpan<byte> segmentData,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2HuffmanTable>? customTables = null)
    {
        if (segmentData.Length < 10)
        {
            return Array.Empty<Jbig2Bitmap>();
        }

        var info = Jbig2SymbolDictionarySegmentInfo.Parse(segmentData);

        // TODO: [LOW] ContextUsed/ContextRetained (ITU-T T.88 Section 7.4.2.2 steps 3/7) is NYI
        if (info.Flags.ContextUsed || info.Flags.ContextRetained)
        {
            throw new NotImplementedException(
                "JBIG2 symbol dictionary: ContextUsed/ContextRetained flags are not implemented.");
        }

        if (info.Flags.UseHuffman)
        {
            return DecodeHuffmanSymbols(segmentData, info, referredSymbols, customTables);
        }

        return DecodeArithmeticSymbols(segmentData, info, referredSymbols);
    }

    private static Jbig2Bitmap[] DecodeArithmeticSymbols(
        in ReadOnlySpan<byte> segmentData,
        in Jbig2SymbolDictionarySegmentInfo info,
        List<Jbig2Bitmap> referredSymbols)
    {
        int newSymbolCount = info.NewSymbolCount;

        if (info.DataOffset >= segmentData.Length)
        {
            throw new InvalidOperationException(
                $"JBIG2 symbol dictionary: data offset {info.DataOffset} exceeds segment length {segmentData.Length}.");
        }

        int symbolCodeLength = Jbig2SymbolCodeLength.Compute(referredSymbols.Count + newSymbolCount);
        Jbig2SymbolArithmeticContext context = new(info, symbolCodeLength);

        return Jbig2SymbolDictionaryArithmeticDecoder.Decode(
            segmentData.Slice(info.DataOffset),
            context,
            referredSymbols);
    }

    private static Jbig2Bitmap[] DecodeHuffmanSymbols(
        in ReadOnlySpan<byte> segmentData,
        in Jbig2SymbolDictionarySegmentInfo info,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2HuffmanTable>? customTables)
    {
        int newSymbolCount = info.NewSymbolCount;

        // 7.4.2.1.6 Symbol dictionary segment Huffman table selection
        int customIndex = 0;

        Jbig2HuffmanTable heightTable = Jbig2StandardHuffmanTables.SelectDeltaHeight(
            info.Flags.HuffDhSelection, customTables, ref customIndex);
        Jbig2HuffmanTable widthTable = Jbig2StandardHuffmanTables.SelectDeltaWidth(
            info.Flags.HuffDwSelection, customTables, ref customIndex);
        Jbig2HuffmanTable bmSizeTable = Jbig2StandardHuffmanTables.SelectBitmapSize(
            info.Flags.HuffBmSizeSelection, customTables, ref customIndex);
        Jbig2HuffmanTable aggInstTable = Jbig2StandardHuffmanTables.SelectAggregateInstances(
            info.Flags.HuffAggInstSelection, customTables, ref customIndex);

        if (info.DataOffset >= segmentData.Length)
        {
            throw new InvalidOperationException(
                $"JBIG2 symbol dictionary: data offset {info.DataOffset} exceeds segment length {segmentData.Length}.");
        }

        ReadOnlySpan<byte> codedData = segmentData.Slice(info.DataOffset);
        Jbig2HuffmanDecoder huffDecoder = new(codedData);

        List<Jbig2Bitmap> newSymbols = new(newSymbolCount);
        int currentHeight = 0;
        List<int> symbolWidths = [];

        // SDREFAGG: per-symbol refinement coding (ITU-T T.88 Section 6.5.8.2)
        bool useRefAgg = info.Flags.UseRefinementAggregation;
        int symbolCodeLength = useRefAgg
            ? Jbig2SymbolCodeLength.Compute(referredSymbols.Count + newSymbolCount)
            : 0;

        // 6.5.5 Decode height classes using Huffman
        while (newSymbols.Count < newSymbolCount)
        {
            // 6.5.6 Decode delta height
            int deltaHeight = huffDecoder.DecodeValue(heightTable);
            if (deltaHeight == int.MinValue)
            {
                break;
            }

            currentHeight += deltaHeight;
            int currentWidth = 0;
            int totalWidth = 0;
            int firstSymbol = symbolWidths.Count;

            // 6.5.7 Decode symbols in this height class (terminates on OOB per spec 6.5.7)
            while (!huffDecoder.IsExhausted)
            {
                int deltaWidth = huffDecoder.DecodeValue(widthTable);
                if (deltaWidth == int.MinValue)
                {
                    // OOB signals end of height class
                    break;
                }

                currentWidth += deltaWidth;
                totalWidth += currentWidth;

                if (useRefAgg)
                {
                    // 6.5.8.2 Each symbol decoded individually via refinement aggregation
                    Jbig2Bitmap symbolBitmap = DecodeRefinementAggregate(
                        huffDecoder,
                        aggInstTable,
                        symbolCodeLength,
                        currentWidth,
                        currentHeight,
                        referredSymbols,
                        newSymbols,
                        info);
                    newSymbols.Add(symbolBitmap);
                }
                else
                {
                    symbolWidths.Add(currentWidth);
                }
            }

            if (useRefAgg)
            {
                // No collective bitmap when SDREFAGG is active
                continue;
            }

            int heightClassCount = symbolWidths.Count - firstSymbol;
            if (heightClassCount == 0)
            {
                continue;
            }

            // 6.5.9 Height class collective bitmap
            int bitmapSize = huffDecoder.DecodeValue(bmSizeTable);
            huffDecoder.ByteAlign();

            Jbig2Bitmap collectiveBitmap;
            if (bitmapSize == 0)
            {
                // Uncompressed collective bitmap
                collectiveBitmap = ReadUncompressedBitmap(huffDecoder, totalWidth, currentHeight);
            }
            else
            {
                // MMR-coded collective bitmap
                int startBytePos = huffDecoder.BitPosition / 8;
                int bitmapEnd = startBytePos + bitmapSize;

                collectiveBitmap = Jbig2MmrDecoder.Decode(
                    codedData.Slice(startBytePos, bitmapSize),
                    totalWidth,
                    currentHeight,
                    out _);

                huffDecoder.SetBytePosition(bitmapEnd);
            }

            // Split collective bitmap into individual symbols
            if (heightClassCount == 1)
            {
                newSymbols.Add(collectiveBitmap);
            }
            else
            {
                int xOffset = 0;
                for (int sw = firstSymbol; sw < symbolWidths.Count; sw++)
                {
                    int w = symbolWidths[sw];
                    if (w <= 0)
                    {
                        newSymbols.Add(Jbig2Bitmap.Empty);
                        continue;
                    }

                    Jbig2Bitmap symbol = new(w, currentHeight);
                    for (int y = 0; y < currentHeight; y++)
                    {
                        for (int x = 0; x < w && (xOffset + x) < collectiveBitmap.Width; x++)
                        {
                            symbol.SetPixel(x, y, collectiveBitmap.GetPixel(xOffset + x, y));
                        }
                    }

                    newSymbols.Add(symbol);
                    xOffset += w;
                }
            }
        }

        if (newSymbols.Count < newSymbolCount)
        {
            throw new InvalidOperationException(
                $"JBIG2 symbol dictionary: decoded {newSymbols.Count} symbols but expected {newSymbolCount}.");
        }

        // 6.5.10 Export symbols
        return ExportSymbols(huffDecoder, Jbig2StandardHuffmanTables.TableB1, referredSymbols, newSymbols);
    }

    /// <summary>
    /// Reads an uncompressed bitmap from the Huffman stream, byte-aligning after each row.
    /// </summary>
    private static Jbig2Bitmap ReadUncompressedBitmap(Jbig2HuffmanDecoder reader, int width, int height)
    {
        Jbig2Bitmap bitmap = new(width, height);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                bitmap.SetPixel(x, y, reader.ReadBit());
            }

            reader.ByteAlign();
        }

        return bitmap;
    }

    /// <summary>
    /// Decodes export flags using Huffman-coded run lengths and returns the exported symbols
    /// (ITU-T T.88 Section 6.5.10).
    /// </summary>
    private static Jbig2Bitmap[] ExportSymbols(
        Jbig2HuffmanDecoder huffDecoder,
        Jbig2HuffmanTable runLengthTable,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2Bitmap> newSymbols)
    {
        int totalSymbols = referredSymbols.Count + newSymbols.Count;
        var exportFlags = new bool[totalSymbols];
        var currentFlag = false;
        int flagIndex = 0;

        while (flagIndex < totalSymbols)
        {
            int runLength = huffDecoder.DecodeValue(runLengthTable);
            if (runLength == int.MinValue)
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

        List<Jbig2Bitmap> exported = [];
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

        // Fall back to all new symbols if export produced nothing
        if (exported.Count == 0 && newSymbols.Count > 0)
        {
            return newSymbols.ToArray();
        }

        return exported.ToArray();
    }

    /// <summary>
    /// Decodes a single symbol via refinement aggregation within a Huffman-coded symbol dictionary
    /// (ITU-T T.88 Section 6.5.8.2). Reads REFAGGNINST from the aggregate instances table, then:
    /// - If 1: single-symbol refinement (6.5.8.2.2) using ID + RDX + RDY + BMSIZE + arithmetic refine.
    /// - If >1: inline text region decode (6.5.8.2.1) using arithmetic placement.
    /// </summary>
    private static Jbig2Bitmap DecodeRefinementAggregate(
        Jbig2HuffmanDecoder huffDecoder,
        Jbig2HuffmanTable aggInstTable,
        int symbolCodeLength,
        int symbolWidth,
        int symbolHeight,
        List<Jbig2Bitmap> referredSymbols,
        List<Jbig2Bitmap> newSymbols,
        in Jbig2SymbolDictionarySegmentInfo info)
    {
        int refAggNinst = huffDecoder.DecodeValue(aggInstTable);

        if (refAggNinst <= 0)
        {
            return Jbig2Bitmap.Empty;
        }

        if (refAggNinst > 1)
        {
            // TODO: [LOW] REFAGGNINST > 1 requires a zero-padded Huffman word stream.
            // This needs a dedicated ReadBit mode that
            // returns 0 past end + loop guards (NSYMSDECODED >= SDNUMNEWSYMS, emptyRuns cap)
            // to prevent infinite loops while still allowing the text region placement decode
            // to complete.
            throw new NotImplementedException(
                "JBIG2 symbol dictionary: Huffman coding with refinement aggregation (REFAGGNINST > 1) is not supported.");
        }

        // 6.5.8.2.2 Single instance refinement
        // (2) Read symbol ID
        int symbolId = huffDecoder.ReadBits(symbolCodeLength);

        // (3-4) Read refinement offsets using fixed tables (B.15 for RDX/RDY per ITU-T T.88 Section B.15)
        int rdx = huffDecoder.DecodeValue(Jbig2StandardHuffmanTables.TableB15);
        int rdy = huffDecoder.DecodeValue(Jbig2StandardHuffmanTables.TableB15);

        // (5) Read bitmap size and byte-align
        int bmSize = huffDecoder.DecodeValue(Jbig2StandardHuffmanTables.TableB1);
        huffDecoder.ByteAlign();

        // Resolve reference bitmap
        int totalAvailable = referredSymbols.Count + newSymbols.Count;
        Jbig2Bitmap referenceBitmap;
        if (symbolId < referredSymbols.Count)
        {
            referenceBitmap = referredSymbols[symbolId];
        }
        else if (symbolId < totalAvailable)
        {
            referenceBitmap = newSymbols[symbolId - referredSymbols.Count];
        }
        else
        {
            return new Jbig2Bitmap(symbolWidth, symbolHeight);
        }

        // (6) Arithmetic-decode refinement from embedded data
        int startByteSingle = huffDecoder.BitPosition / 8;

        // Compute actual bmSize if 0 (uncompressed size = height * stride)
        int actualBmSize = bmSize;
        if (actualBmSize == 0)
        {
            actualBmSize = symbolHeight * ((symbolWidth + 7) / 8);
        }

        ReadOnlySpan<byte> refData = huffDecoder.GetDataSpan().Slice(startByteSingle, actualBmSize);

        Jbig2ArithmeticContext refContext = new(
            0,
            info.Flags.RefinementTemplate,
            info.RefinementAtPixels?.AtX,
            info.RefinementAtPixels?.AtY);

        Jbig2ArithmeticReader refReader = new(refData);
        Jbig2Bitmap refinedBitmap = Jbig2RefinementRegionDecoder.DecodeInline(
            ref refReader,
            refContext,
            symbolWidth,
            symbolHeight,
            referenceBitmap,
            ((symbolWidth - referenceBitmap.Width) / 2) + rdx,
            ((symbolHeight - referenceBitmap.Height) / 2) + rdy);

        // (7) Advance past embedded bitmap data
        huffDecoder.SetBytePosition(startByteSingle + actualBmSize);

        return refinedBitmap;
    }
}
