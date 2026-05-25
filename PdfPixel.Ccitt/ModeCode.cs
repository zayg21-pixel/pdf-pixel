namespace PdfPixel.Ccitt;

/// <summary>
/// Represents a CCITT mode code (bit pattern) along with its type and vertical delta (for vertical modes).
/// </summary>
public readonly struct ModeCode
{
    /// <summary>
    /// Initializes a mode code entry.
    /// </summary>
    /// <param name="bits">Number of bits in the code word.</param>
    /// <param name="code">Bit pattern (MSB-aligned to <paramref name="bits"/> width).</param>
    /// <param name="type">Mode type this code represents.</param>
    /// <param name="delta">Signed vertical delta for <see cref="ModeType.Vertical"/> codes; zero otherwise.</param>
    public ModeCode(int bits, int code, ModeType type, int delta)
    {
        Bits = bits;
        Code = code;
        Type = type;
        VerticalDelta = delta;
    }

    /// <summary>
    /// Number of bits in this code word.
    /// </summary>
    public int Bits { get; }

    /// <summary>
    /// Bit pattern of this code word.
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// Mode type encoded by this code.
    /// </summary>
    public ModeType Type { get; }

    /// <summary>
    /// Signed vertical offset from b1 to a1; only meaningful for <see cref="ModeType.Vertical"/> codes.
    /// </summary>
    public int VerticalDelta { get; }
}
