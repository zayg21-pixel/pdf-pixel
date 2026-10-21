using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of a JBIG2Decode filter.
/// </summary>
public sealed class PdfJbig2DecodeParameters : PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfJbig2DecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfJbig2DecodeParameters(PdfDictionary dictionary)
    {
        PdfObject? globalsObject = dictionary.GetObject(PdfTokens.Jbig2GlobalsKey);
        if (globalsObject != null)
        {
            Jbig2Globals = globalsObject.Stream;
            Jbig2GlobalsReference = globalsObject.Reference;
        }
    }

    /// <summary>
    /// JBIG2 globals stream (/JBIG2Globals), holding the shared symbol dictionaries defined outside the
    /// page stream. Null when no /JBIG2Globals is declared.
    /// </summary>
    public PdfObjectStream? Jbig2Globals { get; }

    /// <summary>
    /// Reference of the object the JBIG2 globals stream was read from, under which its decoded segments
    /// are cached for every image sharing it.
    /// </summary>
    public PdfReference? Jbig2GlobalsReference { get; }
}
