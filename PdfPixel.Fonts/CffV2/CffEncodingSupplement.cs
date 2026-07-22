namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// A single CFF Encoding supplement entry: a character code paired with the SID it selects.
/// </summary>
public readonly struct CffEncodingSupplement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CffEncodingSupplement"/> struct.
    /// </summary>
    /// <param name="code">The character code.</param>
    /// <param name="sid">The SID.</param>
    public CffEncodingSupplement(byte code, ushort sid)
    {
        Code = code;
        Sid = sid;
    }

    /// <summary>
    /// Gets the character code.
    /// </summary>
    public byte Code { get; }

    /// <summary>
    /// Gets the SID.
    /// </summary>
    public ushort Sid { get; }
}
