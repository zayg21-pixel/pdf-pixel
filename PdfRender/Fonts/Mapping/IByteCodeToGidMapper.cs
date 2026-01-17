namespace PdfRender.Fonts.Mapping;

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
}