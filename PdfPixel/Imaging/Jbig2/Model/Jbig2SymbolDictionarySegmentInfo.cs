using System;
using System.Buffers.Binary;

namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Fully parsed header for a JBIG2 symbol dictionary segment (ITU-T T.88 Section 7.4.2).
/// Combines the flag bits from <see cref="Jbig2SymbolDictionaryFlags"/> with the non-flag segment
/// parameters that follow them in the header: AT pixel coordinates, refinement AT pixel
/// coordinates, symbol counts, and the byte offset where coded symbol data begins.
/// </summary>
/// <remarks>
/// Construct via <see cref="Parse"/> rather than directly.
/// </remarks>
internal readonly struct Jbig2SymbolDictionarySegmentInfo
{
    private Jbig2SymbolDictionarySegmentInfo(
        Jbig2SymbolDictionaryFlags flags,
        Jbig2AtPixels? atPixels,
        Jbig2AtPixels? refinementAtPixels,
        int exportedSymbolCount,
        int newSymbolCount,
        int dataOffset)
    {
        Flags = flags;
        AtPixels = atPixels;
        RefinementAtPixels = refinementAtPixels;
        ExportedSymbolCount = exportedSymbolCount;
        NewSymbolCount = newSymbolCount;
        DataOffset = dataOffset;
    }

    /// <summary>Parsed flag bits from the 2-byte segment flags word.</summary>
    public Jbig2SymbolDictionaryFlags Flags { get; }

    /// <summary>
    /// Adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when Huffman coding is active and AT pixels are absent.
    /// </summary>
    public Jbig2AtPixels? AtPixels { get; }

    /// <summary>
    /// Refinement adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when refinement AT pixels are not present.
    /// </summary>
    public Jbig2AtPixels? RefinementAtPixels { get; }

    /// <summary>Number of symbols exported by this segment.</summary>
    public int ExportedSymbolCount { get; }

    /// <summary>Number of new symbols defined by this segment.</summary>
    public int NewSymbolCount { get; }

    /// <summary>
    /// Byte offset from the start of the segment data to the first byte of coded symbol data.
    /// All header fields (flags, AT pixels, counts) precede this offset.
    /// </summary>
    public int DataOffset { get; }

    /// <summary>
    /// Parses the segment header from the supplied data span and returns a populated
    /// <see cref="Jbig2SymbolDictionarySegmentInfo"/>.
    /// </summary>
    /// <param name="data">Segment data beginning at the first byte of the 2-byte flags word.</param>
    /// <returns>Parsed segment info.</returns>
    public static Jbig2SymbolDictionarySegmentInfo Parse(ReadOnlySpan<byte> data)
    {
        ushort flagsWord = (ushort)((data[0] << 8) | data[1]);
        var flags = new Jbig2SymbolDictionaryFlags(flagsWord);

        // Layout after the 2-byte flags word:
        //   [AT pixel pairs  (AtPixelsByteCount bytes, only when !UseHuffman)]
        //   [refinement AT   (RefinementAtPixelsByteCount bytes, conditional)]
        //   exported symbol count (4 bytes)
        //   new symbol count      (4 bytes)
        int offset = 2;
        var atPixels = flags.GetAtPixels(data.Slice(offset));
        offset += flags.AtPixelsByteCount;

        var refinementAtPixels = flags.GetRefinementAtPixels(data.Slice(offset));
        offset += flags.RefinementAtPixelsByteCount;

        int exportedSymbolCount = 0;
        int newSymbolCount = 0;
        if (offset + 7 < data.Length)
        {
            exportedSymbolCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));
            offset += 4;
            newSymbolCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.Slice(offset));
            offset += 4;
        }

        return new Jbig2SymbolDictionarySegmentInfo(
            flags,
            atPixels,
            refinementAtPixels,
            exportedSymbolCount,
            newSymbolCount,
            offset);
    }

    }
