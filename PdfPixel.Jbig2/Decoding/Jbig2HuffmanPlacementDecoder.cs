using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Huffman-coded text region placement decoder (ITU-T T.88 Section 6.4).
/// All methods are static and stateless, paralleling <see cref="Jbig2ArithmeticDecoder"/>.
/// </summary>
internal static class Jbig2HuffmanPlacementDecoder
{
    /// <summary>
    /// Decodes symbol placements from the Huffman-coded data stream and appends them to
    /// <paramref name="placements"/> (ITU-T T.88 Section 6.4.5–6.4.11). The region bitmap is
    /// not materialised here — that happens later via <see cref="Jbig2TextRegionPlacements.Compose"/>.
    /// </summary>
    /// <param name="huffDecoder">Huffman bit reader positioned after the symbol ID table decode.</param>
    /// <param name="symbolIdTable">Pre-built symbol ID Huffman table.</param>
    /// <param name="dtTable">Huffman table for delta T.</param>
    /// <param name="fsTable">Huffman table for first S.</param>
    /// <param name="dsTable">Huffman table for delta S.</param>
    /// <param name="flags">Text region flags (placement parameters).</param>
    /// <param name="symbols">Available symbol bitmaps.</param>
    /// <param name="numberOfSymbolInstances">Total instances to place.</param>
    /// <param name="placements">Sink that records each placed symbol with its region-local coordinates.</param>
    /// <param name="refinement">Refinement tables, or null when refinement is disabled.</param>
    internal static void Decode(
        Jbig2HuffmanDecoder huffDecoder,
        Jbig2HuffmanTable symbolIdTable,
        Jbig2HuffmanTable dtTable,
        Jbig2HuffmanTable fsTable,
        Jbig2HuffmanTable dsTable,
        Jbig2TextRegionFlags flags,
        List<Jbig2Bitmap> symbols,
        int numberOfSymbolInstances,
        Jbig2TextRegionPlacements placements,
        Jbig2RefinementHuffmanTables? refinement = null)
    {
        int stripSize = 1 << flags.LogStripSize;
        bool transposed = flags.Transposed;
        int referenceCorner = flags.ReferenceCorner;
        int dsOffset = flags.SOffset;

        // 6.4.5 Initial strip T
        int stripT = -huffDecoder.DecodeValue(dtTable);
        int firstS = 0;
        int instancesDecoded = 0;

        while (instancesDecoded < numberOfSymbolInstances)
        {
            // 6.4.6 Decode strip delta T
            int deltaT = huffDecoder.DecodeValue(dtTable);
            if (deltaT == int.MinValue)
            {
                break;
            }

            stripT += deltaT;

            // 6.4.7 Decode first S
            int deltaFirstS = huffDecoder.DecodeValue(fsTable);
            if (deltaFirstS == int.MinValue)
            {
                break;
            }

            firstS += deltaFirstS;
            int currentS = firstS;

            // 6.4.8-6.4.11 Decode symbol instances within this strip
            while (true)
            {
                // 6.4.9 Current T within strip
                int currentT = 0;
                if (stripSize > 1)
                {
                    currentT = huffDecoder.ReadBits(flags.LogStripSize);
                }

                int t = stripSize * stripT + currentT;

                // Decode symbol ID
                int symbolId = huffDecoder.DecodeValue(symbolIdTable);
                if (symbolId < 0 || symbolId >= symbols.Count)
                {
                    break;
                }

                var symbolBitmap = symbols[symbolId];
                int symbolWidth = symbolBitmap.Width;
                int symbolHeight = symbolBitmap.Height;

                // 6.4.11 Read refinement flag (1 bit per instance when refinement is enabled)
                if (flags.UseRefinement)
                {
                    int applyRefinement = huffDecoder.ReadBit();
                    if (applyRefinement != 0)
                    {
                        if (refinement == null)
                        {
                            throw new InvalidOperationException(
                                "JBIG2 text region: refinement requested but no refinement tables provided.");
                        }

                        var refTables = refinement.Value;

                        // 6.4.11 (1-4) Decode refinement deltas from Huffman tables
                        int rdw = huffDecoder.DecodeValue(refTables.RdwTable);
                        int rdh = huffDecoder.DecodeValue(refTables.RdhTable);
                        int rdx = huffDecoder.DecodeValue(refTables.RdxTable);
                        int rdy = huffDecoder.DecodeValue(refTables.RdyTable);

                        // 6.4.11 (5) Decode bitmap size and byte-align
                        int bmSize = huffDecoder.DecodeValue(refTables.SizeTable);
                        huffDecoder.ByteAlign();

                        // 6.4.11 (6) Decode refinement bitmap using arithmetic coding
                        int refWidth = symbolWidth + rdw;
                        int refHeight = symbolHeight + rdh;

                        if (refWidth > 0 && refHeight > 0)
                        {
                            int startByte = huffDecoder.BitPosition / 8;
                            ReadOnlySpan<byte> refData = huffDecoder.GetDataSpan().Slice(startByte, bmSize);

                            var refContext = new Jbig2ArithmeticContext(
                                0,
                                refTables.Template,
                                refTables.AtX,
                                refTables.AtY);

                            var refReader = new Jbig2ArithmeticReader(refData);
                            symbolBitmap = Jbig2RefinementRegionDecoder.DecodeInline(
                                ref refReader,
                                refContext,
                                refWidth,
                                refHeight,
                                symbolBitmap,
                                (rdw >> 1) + rdx,
                                (rdh >> 1) + rdy);

                            symbolWidth = refWidth;
                            symbolHeight = refHeight;
                        }

                        // 6.4.11 (7) Advance past the embedded bitmap data
                        huffDecoder.SetBytePosition(huffDecoder.BitPosition / 8 + bmSize);
                    }
                }

                // 6.4.10 Compute placement coordinates
                int increment = 0;
                if (!transposed)
                {
                    if (referenceCorner > 1)
                    {
                        currentS += symbolWidth - 1;
                    }
                    else
                    {
                        increment = symbolWidth - 1;
                    }
                }
                else if ((referenceCorner & 1) == 0)
                {
                    currentS += symbolHeight - 1;
                }
                else
                {
                    increment = symbolHeight - 1;
                }

                int offsetT = t - ((referenceCorner & 1) != 0 ? 0 : symbolHeight - 1);
                int offsetS = currentS - ((referenceCorner & 2) != 0 ? symbolWidth - 1 : 0);

                // Place symbol (region-local coordinates; SBCOMBOP applied later by the placements sink)
                if (transposed)
                {
                    placements.Add(symbolBitmap, offsetT, offsetS);
                }
                else
                {
                    placements.Add(symbolBitmap, offsetS, offsetT);
                }

                instancesDecoded++;
                if (instancesDecoded >= numberOfSymbolInstances)
                {
                    break;
                }

                // 6.4.8 Decode next delta S (OOB means end of strip)
                int deltaS = huffDecoder.DecodeValue(dsTable);
                if (deltaS == int.MinValue)
                {
                    break;
                }

                currentS += increment + deltaS + dsOffset;
            }
        }
    }

    /// <summary>
    /// Decodes the symbol ID Huffman table from the coded data stream
    /// (ITU-T T.88 Section 7.4.3.1.7).
    /// Uses a two-level RUNCODE scheme: first reads 35 4-bit code lengths to build
    /// a RUNCODE table, then uses that table to read variable-length symbol ID codes.
    /// </summary>
    /// <param name="reader">Huffman bit reader positioned at the start of the symbol ID table data.</param>
    /// <param name="symbolCount">Total number of symbols available for placement.</param>
    /// <returns>Built symbol ID Huffman table.</returns>
    internal static Jbig2HuffmanTable DecodeSymbolIdTable(Jbig2HuffmanDecoder reader, int symbolCount)
    {
        // Read 35 RUNCODE lengths (4 bits each)
        var runCodeLines = new Jbig2HuffmanLine[35];
        for (int i = 0; i <= 34; i++)
        {
            int codeLength = reader.ReadBits(4);
            runCodeLines[i] = new Jbig2HuffmanLine(i, 0, codeLength);
        }

        var runCodesTable = Jbig2HuffmanTable.Build(runCodeLines);

        // Decode symbol ID code lengths using RUNCODEs
        var symbolLines = new List<Jbig2HuffmanLine>(symbolCount);
        int symbolIndex = 0;

        while (symbolIndex < symbolCount)
        {
            int codeLength = reader.DecodeValue(runCodesTable);

            if (codeLength >= 32)
            {
                int repeatedLength = 0;
                int numberOfRepeats = 0;

                switch (codeLength)
                {
                    case 32:
                        // Repeat previous length 3-6 times
                        if (symbolIndex == 0)
                        {
                            break;
                        }

                        numberOfRepeats = reader.ReadBits(2) + 3;
                        repeatedLength = symbolLines[symbolIndex - 1].PrefixLength;
                        break;
                    case 33:
                        // Repeat zero 3-10 times
                        numberOfRepeats = reader.ReadBits(3) + 3;
                        repeatedLength = 0;
                        break;
                    case 34:
                        // Repeat zero 11-138 times
                        numberOfRepeats = reader.ReadBits(7) + 11;
                        repeatedLength = 0;
                        break;
                    default:
                        numberOfRepeats = 0;
                        repeatedLength = 0;
                        break;
                }

                for (int j = 0; j < numberOfRepeats && symbolIndex < symbolCount; j++)
                {
                    symbolLines.Add(new Jbig2HuffmanLine(symbolIndex, 0, repeatedLength));
                    symbolIndex++;
                }
            }
            else
            {
                symbolLines.Add(new Jbig2HuffmanLine(symbolIndex, 0, codeLength));
                symbolIndex++;
            }
        }

        reader.ByteAlign();
        return Jbig2HuffmanTable.Build(symbolLines.ToArray());
    }
}
