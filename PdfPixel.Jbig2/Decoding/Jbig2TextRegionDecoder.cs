using System;
using System.Collections.Generic;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Decodes JBIG2 text region segments (ITU-T T.88 Section 6.4).
/// Text regions place symbol instances from dictionaries onto a region bitmap at specified locations.
/// Supports arithmetic-coded placement with optional refinement.
/// </summary>
internal static class Jbig2TextRegionDecoder
{
    /// <summary>
    /// Decodes a text region and returns its captured symbol placements. The caller materialises
    /// the region bitmap and composites it onto the page (or stores it as an intermediate)
    /// via <see cref="Jbig2TextRegionPlacements.Compose"/>.
    /// </summary>
    /// <param name="segmentData">Encoded text region data (starting after region info).</param>
    /// <param name="regionInfo">Region dimensions and location.</param>
    /// <param name="symbols">Available symbol bitmaps (from referred dictionaries).</param>
    /// <param name="customTables">User-defined Huffman tables from referred Table segments.</param>
    /// <returns>Captured symbol placements for this text region.</returns>
    public static Jbig2TextRegionPlacements Decode(
        ReadOnlySpan<byte> segmentData,
        Jbig2RegionHeader regionInfo,
        List<Jbig2Bitmap> symbols,
        List<Jbig2HuffmanTable> customTables = null)
    {
        if (segmentData.Length < 2)
        {
            return new Jbig2TextRegionPlacements(
                regionInfo.Width,
                regionInfo.Height,
                defaultPixel: 0,
                symbolCombinationOperator: Jbig2CombinationOperator.Or);
        }

        var info = Jbig2TextRegionSegmentInfo.Parse(segmentData, symbols.Count);

        if (info.Flags.UseHuffman)
        {
            return DecodeHuffman(segmentData, regionInfo, symbols, info, customTables);
        }

        return DecodeArithmetic(segmentData, regionInfo, symbols, info);
    }

    private static Jbig2TextRegionPlacements DecodeArithmetic(
        ReadOnlySpan<byte> segmentData,
        Jbig2RegionHeader regionInfo,
        List<Jbig2Bitmap> symbols,
        Jbig2TextRegionSegmentInfo info)
    {
        var placements = new Jbig2TextRegionPlacements(
            regionInfo.Width,
            regionInfo.Height,
            info.Flags.DefaultPixel,
            info.Flags.CombinationOperator);

        if (symbols.Count == 0 || info.NumberOfSymbolInstances <= 0)
        {
            return placements;
        }

        // Skip text region header to reach coded data.
        // Layout: flags (2) + [refinement AT (4, when applicable)] + number of instances (4)
        int headerOffset = 2;
        if (info.Flags.UseRefinement && info.Flags.RefinementTemplate == 0)
        {
            headerOffset += 4;
        }

        headerOffset += 4;

        if (headerOffset >= segmentData.Length)
        {
            return placements;
        }

        var context = new Jbig2ArithmeticContext(
            info.SymbolIdCodeLength,
            info.Flags.RefinementTemplate,
            info.RefinementAtPixels?.AtX,
            info.RefinementAtPixels?.AtY);

        context.PlacementFlags = info.Flags;

        var reader = new Jbig2ArithmeticReader(segmentData.Slice(headerOffset));
        Jbig2ArithmeticDecoder.Decode(ref reader, context, placements, symbols, info.NumberOfSymbolInstances);

        return placements;
    }

    private static Jbig2TextRegionPlacements DecodeHuffman(
        ReadOnlySpan<byte> segmentData,
        Jbig2RegionHeader regionInfo,
        List<Jbig2Bitmap> symbols,
        Jbig2TextRegionSegmentInfo info,
        List<Jbig2HuffmanTable> customTables)
    {
        var placements = new Jbig2TextRegionPlacements(
            regionInfo.Width,
            regionInfo.Height,
            info.Flags.DefaultPixel,
            info.Flags.CombinationOperator);

        if (symbols.Count == 0 || info.NumberOfSymbolInstances <= 0)
        {
            return placements;
        }

        // Parse Huffman table selection flags
        // Layout after main flags (2 bytes): huffman flags (2 bytes) + [refinement AT (4)] + instances (4)
        int headerSize = 2; // main flags

        // Huffman flags (2 bytes)
        var huffFlags = new Jbig2TextRegionHuffmanFlags(0);
        if (headerSize + 1 < segmentData.Length)
        {
            ushort huffFlagsWord = (ushort)((segmentData[headerSize] << 8) | segmentData[headerSize + 1]);
            huffFlags = new Jbig2TextRegionHuffmanFlags(huffFlagsWord);
            headerSize += 2;
        }

        // Skip refinement AT pixels if present
        if (info.Flags.UseRefinement && info.Flags.RefinementTemplate == 0)
        {
            headerSize += 4;
        }

        // Number of instances (4 bytes)
        headerSize += 4;

        if (headerSize >= segmentData.Length)
        {
            return placements;
        }

        // 7.4.3.1.6 Text region segment Huffman table selection
        int customIndex = 0;

        var fsTable = Jbig2StandardHuffmanTables.SelectFirstS(huffFlags.FsSelection, customTables, ref customIndex);
        var dsTable = Jbig2StandardHuffmanTables.SelectDeltaS(huffFlags.DsSelection, customTables, ref customIndex);
        var dtTable = Jbig2StandardHuffmanTables.SelectDeltaT(huffFlags.DtSelection, customTables, ref customIndex);

        // Select refinement tables when refinement is enabled
        Jbig2RefinementHuffmanTables? refinement = null;
        if (info.Flags.UseRefinement)
        {
            refinement = new Jbig2RefinementHuffmanTables(
                rdwTable: Jbig2StandardHuffmanTables.SelectRefinementDimension(
                    huffFlags.RefinementDwSelection, customTables, ref customIndex),
                rdhTable: Jbig2StandardHuffmanTables.SelectRefinementDimension(
                    huffFlags.RefinementDhSelection, customTables, ref customIndex),
                rdxTable: Jbig2StandardHuffmanTables.SelectRefinementDimension(
                    huffFlags.RefinementDxSelection, customTables, ref customIndex),
                rdyTable: Jbig2StandardHuffmanTables.SelectRefinementDimension(
                    huffFlags.RefinementDySelection, customTables, ref customIndex),
                sizeTable: Jbig2StandardHuffmanTables.SelectRefinementSize(
                    huffFlags.RefinementSizeSelector ? 1 : 0, customTables, ref customIndex),
                template: info.Flags.RefinementTemplate,
                atX: info.RefinementAtPixels?.AtX,
                atY: info.RefinementAtPixels?.AtY);
        }

        // Create Huffman reader from coded data
        ReadOnlySpan<byte> codedData = segmentData.Slice(headerSize);
        var huffDecoder = new Jbig2HuffmanDecoder(codedData);

        // 7.4.3.1.7 Symbol ID Huffman table decoding
        var symbolIdTable = Jbig2HuffmanPlacementDecoder.DecodeSymbolIdTable(huffDecoder, symbols.Count);

        // Decode text region placement (ITU-T T.88 Section 6.4)
        Jbig2HuffmanPlacementDecoder.Decode(
            huffDecoder,
            symbolIdTable,
            dtTable,
            fsTable,
            dsTable,
            info.Flags,
            symbols,
            info.NumberOfSymbolInstances,
            placements,
            refinement);

        return placements;
    }

    /// <summary>
    /// Decodes a text region inline using a shared arithmetic reader, for multi-instance
    /// aggregate coding in symbol dictionaries (ITU-T T.88 Section 6.5.8.2).
    /// All context arrays (placement and shared) persist across aggregate symbol decodes
    /// within the same symbol dictionary session. Returns the captured placements; the caller
    /// materialises the aggregate bitmap via <see cref="Jbig2TextRegionPlacements.Compose"/>.
    /// </summary>
    /// <param name="reader">Shared arithmetic reader from the symbol dictionary.</param>
    /// <param name="width">Aggregate region width.</param>
    /// <param name="height">Aggregate region height.</param>
    /// <param name="numberOfSymbolInstances">Number of symbol instances to place.</param>
    /// <param name="symbols">Combined symbol pool (referred + new).</param>
    /// <param name="context">Aggregate coding context owned by the symbol dictionary decoder.</param>
    /// <returns>Captured symbol placements for the aggregate region.</returns>
    internal static Jbig2TextRegionPlacements DecodeTextRegionInline(
        ref Jbig2ArithmeticReader reader,
        int width,
        int height,
        int numberOfSymbolInstances,
        List<Jbig2Bitmap> symbols,
        Jbig2ArithmeticContext context)
    {
        var inlineFlags = Jbig2TextRegionFlags.DefaultInlineFlags;
        var placements = new Jbig2TextRegionPlacements(
            width,
            height,
            inlineFlags.DefaultPixel,
            inlineFlags.CombinationOperator);

        if (symbols.Count == 0 || numberOfSymbolInstances <= 0)
        {
            return placements;
        }

        // Per ITU-T T.88 Section 6.5, all context arrays (including placement
        // contexts IADT, IAFS, IADS, IAIT, IARI, IARDW, IARDH) persist across aggregate
        // symbol decodes within the same symbol dictionary session. Do NOT reset them here.
        // stripSize=1, transposed=false, dsOffset=0, referenceCorner=1 (top-left), combinationOperator=OR
        context.PlacementFlags = inlineFlags;

        Jbig2ArithmeticDecoder.Decode(ref reader, context, placements, symbols, numberOfSymbolInstances);

        return placements;
    }
}
