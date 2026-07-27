namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Represents a parsed CFF Encoding: the character-code-to-GID assignment for a name-keyed font.
/// </summary>
public class CffEncoding
{
    /// <summary>
    /// Gets or sets the encoding table format (0 or 1). Null if predefined.
    /// </summary>
    public byte? Format { get; set; }

    /// <summary>
    /// Gets or sets the predefined encoding ID (0 = Standard, 1 = Expert). Null if explicit.
    /// </summary>
    public int? PredefinedId { get; set; }

    /// <summary>
    /// Gets or sets the main table's codes, in read order. Position <c>i</c> is the code for GID <c>i + 1</c>.
    /// Null if predefined.
    /// </summary>
    public byte[]? MainTableCodes { get; set; }

    /// <summary>
    /// Gets or sets the supplemental code-to-SID entries, in read order. Null if there is no supplement.
    /// </summary>
    public CffEncodingSupplement[]? Supplements { get; set; }

    /// <summary>
    /// Gets or sets the GID assigned to each character code (0-255), indexed by code.
    /// </summary>
    public ushort[] GidByCode { get; set; } = new ushort[256];
}
