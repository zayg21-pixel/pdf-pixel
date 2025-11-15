namespace PdfReader.Imaging.Ccitt;

/// <summary>
/// CCITT 1-D run-length decoding (T.4 / T.6) for white / black tables.
/// Produces a sequence consisting of zero or more make-up codes (64..2560) followed by exactly one terminating code (0..63),
/// or an End Of Line (EOL) code. Extended make-up codes are included in the tables.
/// </summary>
internal static class CcittRunDecoder
{
    private const int MaxCodeBits = 13;

    /// <summary>
    /// Decode a single run: zero or more make-up codes then one terminating code. Returns result.
    /// Returns Length = -1 on error. EOL returns IsEndOfLine=true, Length=0, HasTerminating=false.
    /// </summary>
    public static RunDecodeResult DecodeRun(ref CcittBitReader reader, bool isBlack)
    {
        var lookupTable = isBlack ? CcittCodeTables.BlackLookup : CcittCodeTables.WhiteLookup;
        int total = 0;

        while (true)
        {
            var code = DecodeSingleCode(ref reader, lookupTable);
            if (code == null)
            {
                return new RunDecodeResult(-1, false, false);
            }

            if (code.Value.IsEndOfLine)
            {
                if (total != 0)
                {
                    return new RunDecodeResult(-1, false, false);
                }
                return new RunDecodeResult(0, false, true);
            }

            if (code.Value.IsMakeUp)
            {
                total += code.Value.RunLength;
                continue;
            }

            total += code.Value.RunLength;
            return new RunDecodeResult(total, true, false);
        }
    }

    /// <summary>
    /// Decode a single code from the bit stream using a flat lookup table for performance.
    /// </summary>
    public static CcittFaxCode? DecodeSingleCode(ref CcittBitReader reader, CcittFaxCode[] lookupTable)
    {
        int bits = reader.PeekBits(MaxCodeBits);
        CcittFaxCode code = lookupTable[bits];

        for (int i = 0; i < code.BitLength; i++)
        {
            if (reader.ReadBit() < 0)
            {
                return null;
            }
        }

        return code;
    }
}
