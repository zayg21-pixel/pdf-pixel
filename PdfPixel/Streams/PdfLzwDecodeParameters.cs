using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of an LZWDecode filter: the predictor shared with FlateDecode and the code length change.
/// </summary>
public sealed class PdfLzwDecodeParameters : PdfFlateDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfLzwDecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfLzwDecodeParameters(PdfDictionary dictionary)
        : base(dictionary)
    {
        EarlyChange = dictionary.GetInteger(PdfTokens.EarlyChangeKey);
    }

    /// <summary>
    /// Whether the code length increases one code early (/EarlyChange): 1 = early, 0 = postponed. Null when absent.
    /// </summary>
    public int? EarlyChange { get; }
}
