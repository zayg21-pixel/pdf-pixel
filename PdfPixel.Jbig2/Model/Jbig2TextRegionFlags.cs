using System;

namespace PdfPixel.Jbig2.Model;

/// <summary>
/// Parsed flag bits from a JBIG2 text region segment header (ITU-T T.88 Section 7.4.3.1.1).
/// Accepts the raw 2-byte big-endian flags word and exposes each field as a typed, read-only property.
/// </summary>
/// <remarks>
/// Refinement adaptive template (AT) pixel coordinates are not flag bits; they are a
/// data-dependent byte sequence that follows any Huffman table flags in the segment header.
/// Read them via <c>Jbig2Templates.ReadAtPixelPairs</c> and advance the offset by 4 when
/// <see cref="UseRefinement"/> is <see langword="true"/> and <see cref="RefinementTemplate"/> is 0.
/// </remarks>
internal readonly struct Jbig2TextRegionFlags
{
    /// <summary>
    /// Initialises the flags struct by parsing the raw 2-byte big-endian flags word.
    /// </summary>
    /// <param name="flagsWord">The 2-byte flags word read from the segment header.</param>
    public Jbig2TextRegionFlags(ushort flagsWord)
    {
        UseHuffman = (flagsWord & 0x0001) != 0;
        UseRefinement = (flagsWord & 0x0002) != 0;
        LogStripSize = (flagsWord >> 2) & 0x03;
        ReferenceCorner = (flagsWord >> 4) & 0x03;
        Transposed = (flagsWord & 0x0040) != 0;
        CombinationOperator = (Jbig2CombinationOperator)((flagsWord >> 7) & 0x03);
        DefaultPixel = (byte)((flagsWord >> 9) & 1);
        RefinementTemplate = (flagsWord >> 15) & 1;

        // S offset: 5-bit signed value at bits 10–14; sign-extend from bit 4.
        int rawSOffset = (flagsWord >> 10) & 0x1F;
        SOffset = rawSOffset >= 16 ? rawSOffset - 32 : rawSOffset;
    }

    /// <summary>Whether Huffman coding is used for symbol placement (false = arithmetic).</summary>
    public bool UseHuffman { get; }

    /// <summary>Whether refinement coding is applied to placed symbols.</summary>
    public bool UseRefinement { get; }

    /// <summary>Strip size log2 value (LOGSBSTRIPS, bits 2–3).</summary>
    public int LogStripSize { get; }

    /// <summary>Reference corner for symbol placement (bits 4–5, 0–3).</summary>
    public int ReferenceCorner { get; }

    /// <summary>Whether symbols are placed transposed (bit 6).</summary>
    public bool Transposed { get; }

    /// <summary>Combination operator for compositing symbols onto the region (bits 7–8).</summary>
    public Jbig2CombinationOperator CombinationOperator { get; }

    /// <summary>Default pixel value for the region background (bit 9).</summary>
    public byte DefaultPixel { get; }

    /// <summary>Signed S offset value for strip positioning (bits 10–14, range –16..+15).</summary>
    public int SOffset { get; }

    /// <summary>Refinement template selector (bit 15, value 0 or 1).</summary>
    public int RefinementTemplate { get; }

    /// <summary>
    /// Strip size (SBSTRIPS = 1 &lt;&lt; <see cref="LogStripSize"/>).
    /// </summary>
    public int StripSize => 1 << LogStripSize;

    /// <summary>
    /// Fixed placement flags used for multi-instance aggregate decoding (ITU-T T.88 Section 6.5.8.2).
    /// Encodes: <see cref="UseRefinement"/> = <see langword="true"/>, <see cref="StripSize"/> = 1,
    /// <see cref="ReferenceCorner"/> = 1 (top-right), <see cref="Transposed"/> = <see langword="false"/>,
    /// <see cref="SOffset"/> = 0, <see cref="CombinationOperator"/> = <see cref="Jbig2CombinationOperator.Or"/>.
    /// </summary>
    public static readonly Jbig2TextRegionFlags DefaultInlineFlags = new Jbig2TextRegionFlags(0x0012);

}
