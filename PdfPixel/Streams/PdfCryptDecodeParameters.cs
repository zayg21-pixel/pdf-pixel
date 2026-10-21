using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of a Crypt filter.
/// </summary>
public sealed class PdfCryptDecodeParameters : PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCryptDecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfCryptDecodeParameters(PdfDictionary dictionary)
    {
        Type = dictionary.GetName(PdfTokens.TypeKey);
        Name = dictionary.GetName(PdfTokens.NameKey);
    }

    /// <summary>
    /// Dictionary type (/Type), or null when absent.
    /// </summary>
    public PdfString? Type { get; }

    /// <summary>
    /// Name of the crypt filter used to decrypt the stream (/Name), or null when absent.
    /// </summary>
    public PdfString? Name { get; }
}
