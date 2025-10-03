namespace PdfReader.Rendering.Image.Ccitt
{
    /// <summary>
    /// CCITT 1-D run-length decoding (T.4 / T.6) for white / black tables.
    /// Produces a sequence consisting of zero or more make-up codes (64..2560) followed by exactly one terminating code (0..63),
    /// or an End Of Line (EOL) code. Extended make-up codes are included in the tables.
    /// </summary>
    internal static class CcittRunDecoder
    {
        private const int MaxCodeBits = 13;

        internal readonly struct RunDecodeResult
        {
            public readonly int Length;            // Accumulated run length (sum of make-ups + terminating) or 0 if EOL
            public readonly bool HasTerminating;   // True if a terminating code (0..63) closed the run
            public readonly bool IsEndOfLine;      // True if EOL code encountered (no pixels implied)
            public RunDecodeResult(int length, bool hasTerminating, bool isEndOfLine)
            {
                Length = length;
                HasTerminating = hasTerminating;
                IsEndOfLine = isEndOfLine;
            }
        }

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

                if (code.IsEndOfLine)
                {
                    if (total != 0)
                    {
                        return new RunDecodeResult(-1, false, false);
                    }
                    return new RunDecodeResult(0, false, true);
                }

                if (code.IsMakeUp)
                {
                    total += code.RunLength;
                    continue;
                }

                total += code.RunLength;
                return new RunDecodeResult(total, true, false);
            }
        }

        /// <summary>
        /// Decode a single code from the bit stream using a flat lookup table for performance.
        /// </summary>
        public static CcittFaxCode DecodeSingleCode(ref CcittBitReader reader, CcittFaxCode[] lookupTable)
        {
            int bits = reader.PeekBits(MaxCodeBits);
            CcittFaxCode code = lookupTable[bits];
            if (code != null)
            {
                for (int i = 0; i < code.BitLength; i++)
                {
                    if (reader.ReadBit() < 0)
                    {
                        return null;
                    }
                }
                return code;
            }
            return null;
        }
    }
}
