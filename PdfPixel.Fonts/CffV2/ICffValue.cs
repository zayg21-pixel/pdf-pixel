namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Represents a value read from a CFF DICT or charstring, providing its type information.
/// </summary>
public interface ICffValue
{
    /// <summary>
    /// Gets the type of the CFF value.
    /// </summary>
    CffValueType Type { get; }
}
