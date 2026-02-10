namespace PdfPixel.Imaging.Ccitt;

public enum ModeType
{
    Pass,
    Horizontal,
    Vertical
}

/// <summary>
/// Represents a CCITT mode code (bit pattern) along with its type and vertical delta (for vertical modes).
/// </summary>
public readonly struct ModeCode
{
    public ModeCode(int bits, int code, ModeType type, int delta)
    {
        Bits = bits;
        Code = code;
        Type = type;
        VerticalDelta = delta;
    }

    public int Bits { get; }

    public int Code { get; }

    public ModeType Type { get; }

    public int VerticalDelta { get; }
}
