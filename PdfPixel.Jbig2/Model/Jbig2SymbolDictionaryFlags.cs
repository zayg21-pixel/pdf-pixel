using System;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Parsed flag bits from a JBIG2 symbol dictionary segment header (ITU-T T.88 Section 7.4.2.1).
/// Accepts the raw 2-byte big-endian flags word and exposes each field as a typed, read-only property.
/// </summary>
/// <remarks>
/// Adaptive template (AT) pixel coordinates and refinement AT pixel coordinates are not flag bits;
/// they are data-dependent byte sequences that follow the flags word in the segment header.
/// Use <see cref="GetAtPixels"/> and <see cref="GetRefinementAtPixels"/> to read them,
/// passing the appropriate slice of the segment data.
/// </remarks>
internal readonly struct Jbig2SymbolDictionaryFlags
{
    /// <summary>
    /// Initialises the flags struct by parsing the raw 2-byte big-endian flags word.
    /// </summary>
    /// <param name="flagsWord">The 2-byte flags word read from the segment header.</param>
    public Jbig2SymbolDictionaryFlags(ushort flagsWord)
    {
        UseHuffman = (flagsWord & 0x0001) != 0;
        UseRefinementAggregation = (flagsWord & 0x0002) != 0;
        HuffDhSelection = (flagsWord >> 2) & 0x03;
        HuffDwSelection = (flagsWord >> 4) & 0x03;
        HuffBmSizeSelection = (flagsWord >> 6) & 0x01;
        HuffAggInstSelection = (flagsWord >> 7) & 0x01;
        ContextUsed = (flagsWord & 0x0100) != 0;
        ContextRetained = (flagsWord & 0x0200) != 0;
        Template = (flagsWord >> 10) & 0x03;
        RefinementTemplate = (flagsWord >> 12) & 0x01;
    }

    /// <summary>Whether Huffman coding is used (false = arithmetic).</summary>
    public bool UseHuffman { get; }

    /// <summary>Whether refinement/aggregate coding is used.</summary>
    public bool UseRefinementAggregation { get; }

    /// <summary>Huffman DH table selection (bits 2–3).</summary>
    public int HuffDhSelection { get; }

    /// <summary>Huffman DW table selection (bits 4–5).</summary>
    public int HuffDwSelection { get; }

    /// <summary>Huffman bitmap size table selection (bit 6).</summary>
    public int HuffBmSizeSelection { get; }

    /// <summary>Huffman aggregate instances table selection (bit 7).</summary>
    public int HuffAggInstSelection { get; }

    /// <summary>
    /// Whether bitmap coding contexts from a referred-to segment should be used
    /// instead of creating fresh contexts (bit 8, ITU-T T.88 Section 7.4.2.2 step 3).
    /// </summary>
    public bool ContextUsed { get; }

    /// <summary>
    /// Whether bitmap coding contexts should be retained after decoding for use by
    /// subsequent segments (bit 9, ITU-T T.88 Section 7.4.2.2 step 7).
    /// </summary>
    public bool ContextRetained { get; }

    /// <summary>Template for direct-coded symbols (0–3).</summary>
    public int Template { get; }

    /// <summary>Refinement template (0–1).</summary>
    public int RefinementTemplate { get; }

    /// <summary>
    /// Number of bytes occupied by adaptive template (AT) pixel pairs immediately after the
    /// 2-byte flags word in the segment header.
    /// Zero when Huffman coding is active (AT pixels are not present).
    /// </summary>
    public int AtPixelsByteCount
    {
        get
        {
            if (UseHuffman)
            {
                return 0;
            }

            int atCount = Template == 0 ? 4 : 1;
            return atCount * 2;
        }
    }

    /// <summary>
    /// Number of bytes occupied by refinement adaptive template pixel pairs in the segment header.
    /// Zero when refinement aggregation is disabled or refinement template is not 0.
    /// Present regardless of Huffman coding mode when refinement is active with template 0.
    /// </summary>
    public int RefinementAtPixelsByteCount
    {
        get
        {
            if (UseRefinementAggregation && RefinementTemplate == 0)
            {
                return 4;
            }

            return 0;
        }
    }

    /// <summary>
    /// Reads the adaptive template (AT) pixel coordinate pairs from the supplied data slice.
    /// The slice must begin at the first AT pixel byte, which is immediately after the 2-byte flags word.
    /// </summary>
    /// <param name="data">Segment data starting at the first AT pixel byte.</param>
    /// <returns>
    /// AT pixel coordinates, or <see langword="null"/> when Huffman coding is active and AT pixels
    /// are not present.
    /// </returns>
    public Jbig2AtPixels? GetAtPixels(ReadOnlySpan<byte> data)
    {
        if (UseHuffman)
        {
            return null;
        }

        int atCount = Template == 0 ? 4 : 1;
        var atX = new sbyte[atCount];
        var atY = new sbyte[atCount];

        for (int i = 0; i < atCount; i++)
        {
            int byteIndex = i * 2;
            if (byteIndex + 1 < data.Length)
            {
                atX[i] = (sbyte)data[byteIndex];
                atY[i] = (sbyte)data[byteIndex + 1];
            }
        }// TODO: looks similar to another

        return new Jbig2AtPixels(atX, atY);
    }

    /// <summary>
    /// Reads the refinement adaptive template pixel coordinate pairs from the supplied data slice.
    /// The slice must begin immediately after the AT pixel bytes (i.e. at offset
    /// <c>2 + <see cref="AtPixelsByteCount"/></c> from the start of the segment header).
    /// </summary>
    /// <param name="data">Segment data starting at the first refinement AT pixel byte.</param>
    /// <returns>
    /// Refinement AT pixel coordinates, or <see langword="null"/> when refinement AT pixels are not
    /// present (Huffman active, aggregation disabled, or template ≠ 0).
    /// </returns>
    public Jbig2AtPixels? GetRefinementAtPixels(ReadOnlySpan<byte> data)
    {
        if (!UseRefinementAggregation || RefinementTemplate != 0)
        {
            return null;
        }

        var atX = new sbyte[2];
        var atY = new sbyte[2];

        if (data.Length >= 4)
        {
            atX[0] = (sbyte)data[0];
            atY[0] = (sbyte)data[1];
            atX[1] = (sbyte)data[2];
            atY[1] = (sbyte)data[3];
        }

        return new Jbig2AtPixels(atX, atY);
    }
}
