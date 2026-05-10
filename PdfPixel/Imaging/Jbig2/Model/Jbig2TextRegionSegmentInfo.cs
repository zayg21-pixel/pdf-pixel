using System;
using System.Buffers.Binary;

namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Fully parsed header for a JBIG2 text region segment (ITU-T T.88 Section 7.4.3).
/// Combines the flag bits from <see cref="Jbig2TextRegionFlags"/> with the non-flag segment
/// parameters that follow them in the header: refinement AT pixel coordinates and the
/// number of symbol instances.
/// </summary>
/// <remarks>
/// Construct via <see cref="Parse"/> rather than directly.
/// </remarks>
internal readonly struct Jbig2TextRegionSegmentInfo
{
    private Jbig2TextRegionSegmentInfo(
        Jbig2TextRegionFlags flags,
        Jbig2AtPixels? refinementAtPixels,
        int numberOfSymbolInstances,
        int symbolIdCodeLength)
    {
        Flags = flags;
        RefinementAtPixels = refinementAtPixels;
        NumberOfSymbolInstances = numberOfSymbolInstances;
        SymbolIdCodeLength = symbolIdCodeLength;
    }

    /// <summary>Parsed flag bits from the 2-byte segment flags word.</summary>
    public Jbig2TextRegionFlags Flags { get; }

    /// <summary>
    /// Refinement adaptive template pixel coordinates read from the segment header, or
    /// <see langword="null"/> when refinement AT pixels are not present.
    /// </summary>
    public Jbig2AtPixels? RefinementAtPixels { get; }

    /// <summary>Total number of symbol instances to place in the region.</summary>
    public int NumberOfSymbolInstances { get; }

    /// <summary>Symbol ID code length in bits, derived from the total available symbol count.</summary>
    public int SymbolIdCodeLength { get; }

    /// <summary>
    /// Parses the segment header from the supplied data span and returns a populated
    /// <see cref="Jbig2TextRegionSegmentInfo"/>.
    /// </summary>
    /// <param name="data">Segment data beginning at the first byte of the 2-byte flags word.</param>
    /// <param name="totalSymbolCount">Total number of symbols available for placement.</param>
    /// <returns>Parsed segment info.</returns>
    public static Jbig2TextRegionSegmentInfo Parse(ReadOnlySpan<byte> data, int totalSymbolCount)
    {
        ushort flagsWord = (ushort)((data[0] << 8) | data[1]);
        var flags = new Jbig2TextRegionFlags(flagsWord);

        // Layout after the 2-byte flags word:
        //   [Huffman table flags (2 bytes, only when UseHuffman)]
        //   [refinement AT pixels (4 bytes, only when UseRefinement && RefinementTemplate == 0)]
        //   number of symbol instances (4 bytes)
        int offset = 2;
        if (flags.UseHuffman)
        {
            offset += 2;
        }

        var refinementAtPixels = flags.GetRefinementAtPixels(data.Slice(offset));
        offset += flags.RefinementAtPixelsByteCount;

        int numberOfSymbolInstances = 0;
        if (data.Length >= offset + 4)
        {
            numberOfSymbolInstances = BinaryPrimitives.ReadInt32BigEndian(data.Slice(offset));
        }

        return new Jbig2TextRegionSegmentInfo(
            flags,
            refinementAtPixels,
            numberOfSymbolInstances,
            Jbig2SymbolCodeLength.Compute(totalSymbolCount));
    }

    }
