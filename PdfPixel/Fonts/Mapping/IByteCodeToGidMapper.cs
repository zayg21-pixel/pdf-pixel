namespace PdfPixel.Fonts.Mapping;

/// <summary>
/// Maps byte codes to glyph IDs for single-byte fonts.
/// </summary>
public interface IByteCodeToGidMapper
{
    /// <summary>
    /// Retrieves the glyph ID corresponding to the given byte code.
    /// </summary>
    /// <param name="code">Byte code to retrieve the glyph ID for.</param>
    /// <returns>Glyph ID corresponding to the byte code.</returns>
    ushort GetGid(byte code);

    /// <summary>
    /// Retrieves the glyph width corresponding to the given byte code.
    /// </summary>
    /// <param name="code">Byte code to retrieve the glyph width for.</param>
    /// <returns>Glyph width corresponding to the byte code, or 0 if not available.</returns>
    float GetWidth(byte code);
}