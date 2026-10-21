using PdfPixel.Objects;

namespace PdfPixel.Streams;

/// <summary>
/// Base class for the parsed /DecodeParms entry of one filter in a stream's filter chain.
/// The derived type is decided by the filter the entry belongs to.
/// </summary>
public abstract class PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfDecodeParameters"/> class.
    /// </summary>
    private protected PdfDecodeParameters()
    {
    }

    /// <summary>
    /// Parses the /DecodeParms dictionary of <paramref name="filter"/> into the parameters type of that filter.
    /// </summary>
    /// <param name="filter">Filter the dictionary belongs to.</param>
    /// <param name="dictionary">The /DecodeParms dictionary, or null when absent.</param>
    /// <returns>The parameters, or null when the dictionary is absent or the filter takes no parameters.</returns>
    internal static PdfDecodeParameters? FromDictionary(PdfFilterType filter, PdfDictionary? dictionary)
    {
        if (dictionary == null)
        {
            return null;
        }

        return filter switch
        {
            PdfFilterType.FlateDecode => new PdfFlateDecodeParameters(dictionary),
            PdfFilterType.LZWDecode => new PdfLzwDecodeParameters(dictionary),
            PdfFilterType.CCITTFaxDecode => new PdfCcittFaxDecodeParameters(dictionary),
            PdfFilterType.JBIG2Decode => new PdfJbig2DecodeParameters(dictionary),
            PdfFilterType.DCTDecode => new PdfDctDecodeParameters(dictionary),
            PdfFilterType.Crypt => new PdfCryptDecodeParameters(dictionary),
            _ => null
        };
    }
}
