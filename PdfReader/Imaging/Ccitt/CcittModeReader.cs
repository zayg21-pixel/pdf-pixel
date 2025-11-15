namespace PdfReader.Imaging.Ccitt;

/// <summary>
/// Provides decoding logic for CCITT fax mode codes (Pass, Horizontal, Vertical) using a lookup table.
/// Used to interpret bit patterns from a <see cref="CcittBitReader"/> and identify mode transitions in CCITT Group3/4 image streams.
/// </summary>
internal static class CcittModeReader
{
    private static readonly ModeCode[] Codes = new ModeCode[]
    {
        new ModeCode(4, 0b0001, ModeType.Pass, 0),
        new ModeCode(3, 0b001, ModeType.Horizontal, 0),
        new ModeCode(1, 0b1, ModeType.Vertical, 0),
        new ModeCode(3, 0b011, ModeType.Vertical, +1),
        new ModeCode(3, 0b010, ModeType.Vertical, -1),
        new ModeCode(6, 0b000011, ModeType.Vertical, +2),
        new ModeCode(6, 0b000010, ModeType.Vertical, -2),
        new ModeCode(7, 0b0000011, ModeType.Vertical, +3),
        new ModeCode(7, 0b0000010, ModeType.Vertical, -3)
    };

    private const int MaxBits = 7;
    private const int LookupTableSize = 1 << MaxBits;
    private static readonly ModeCode?[] ModeLookup = BuildModeLookupTable();

    private static ModeCode?[] BuildModeLookupTable()
    {
        var lookup = new ModeCode?[LookupTableSize];
        for (int i = 0; i < Codes.Length; i++)
        {
            ModeCode code = Codes[i];
            int prefix = code.Code << MaxBits - code.Bits;
            int fillCount = 1 << MaxBits - code.Bits;
            for (int j = 0; j < fillCount; j++)
            {
                lookup[prefix | j] = code;
            }
        }
        return lookup;
    }

    /// <summary>
    /// Attempts to decode a CCITT mode code from the bit stream.
    /// Peeks up to 7 bits and, if a valid mode code is found, consumes the corresponding bits from the reader.
    /// </summary>
    /// <param name="reader">Reference to the bit reader positioned at the current bit stream location.</param>
    /// <param name="mode">When successful, receives the decoded <see cref="ModeCode"/> value.</param>
    /// <returns>True if a valid mode code was found and consumed; otherwise, false.</returns>
    public static bool TryPeekAndConsumeMode(ref CcittBitReader reader, out ModeCode mode)
    {
        int bits = reader.PeekBits(MaxBits);
        ModeCode? found = ModeLookup[bits];
        if (found.HasValue)
        {
            ModeCode candidate = found.Value;
            int pattern = reader.PeekBits(candidate.Bits);
            if (pattern == candidate.Code)
            {
                for (int b = 0; b < candidate.Bits; b++)
                {
                    if (reader.ReadBit() < 0)
                    {
                        mode = default;
                        return false;
                    }
                }
                mode = candidate;
                return true;
            }
        }
        mode = default;
        return false;
    }
}
