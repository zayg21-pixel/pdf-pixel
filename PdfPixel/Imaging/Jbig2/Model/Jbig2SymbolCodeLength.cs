namespace PdfPixel.Imaging.Jbig2.Model;

/// <summary>
/// Computes the SYMCODELEN value used throughout JBIG2 arithmetic coding contexts
/// (ITU-T T.88 Sections 6.4 and 6.5).
/// SYMCODELEN is the minimum number of bits required to represent any symbol ID in a
/// combined symbol pool and determines the size of the IAID probability context array.
/// </summary>
internal static class Jbig2SymbolCodeLength
{
    /// <summary>
    /// Returns the minimum number of bits needed to represent any symbol ID in a pool of
    /// <paramref name="symbolCount"/> symbols (SYMCODELEN per ITU-T T.88).
    /// </summary>
    /// <param name="symbolCount">Total number of symbols in the combined pool.</param>
    /// <returns>Code length in bits; always at least 1.</returns>
    public static int Compute(int symbolCount)
    {
        if (symbolCount <= 1)
        {
            return 0;
        }

        int bits = 0;
        int value = symbolCount - 1;
        while (value > 0)
        {
            bits++;
            value >>= 1;
        }

        return bits;
    }
}
