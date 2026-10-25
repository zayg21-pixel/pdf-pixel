using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of a CCITTFaxDecode filter.
/// </summary>
public sealed class PdfCcittFaxDecodeParameters : PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCcittFaxDecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfCcittFaxDecodeParameters(PdfDictionary dictionary)
    {
        CompressionScheme = dictionary.GetInteger(PdfTokens.KKey);
        EndOfLine = dictionary.GetBoolean(PdfTokens.EndOfLineKey);
        EncodedByteAlign = dictionary.GetBoolean(PdfTokens.EncodedByteAlignKey);
        Columns = dictionary.GetInteger(PdfTokens.ColumnsKey);
        Rows = dictionary.GetInteger(PdfTokens.RowsKey);
        EndOfBlock = dictionary.GetBoolean(PdfTokens.EndOfBlockKey);
        BlackIs1 = dictionary.GetBoolean(PdfTokens.BlackIs1Key);
        DamagedRowsBeforeError = dictionary.GetInteger(PdfTokens.DamagedRowsBeforeErrorKey);
    }

    /// <summary>
    /// Compression scheme (/K): 0 = Group 3 1-D, &gt;0 = Group 3 2-D, &lt;0 = Group 4 2-D. Null when absent.
    /// </summary>
    public int? CompressionScheme { get; }

    /// <summary>
    /// Whether each row ends with an end-of-line marker (/EndOfLine), or null when absent.
    /// </summary>
    public bool? EndOfLine { get; }

    /// <summary>
    /// Whether each encoded row begins on a byte boundary (/EncodedByteAlign), or null when absent.
    /// </summary>
    public bool? EncodedByteAlign { get; }

    /// <summary>
    /// Width of the image in pixels (/Columns), or null when absent.
    /// </summary>
    public int? Columns { get; }

    /// <summary>
    /// Height of the image in scan lines (/Rows), or null when absent.
    /// </summary>
    public int? Rows { get; }

    /// <summary>
    /// Whether the data ends with an end-of-block pattern (/EndOfBlock), or null when absent.
    /// </summary>
    public bool? EndOfBlock { get; }

    /// <summary>
    /// Whether 1 bits are black pixels and 0 bits white (/BlackIs1), or null when absent.
    /// </summary>
    public bool? BlackIs1 { get; }

    /// <summary>
    /// Number of damaged rows tolerated before an error is reported (/DamagedRowsBeforeError), or null when absent.
    /// </summary>
    public int? DamagedRowsBeforeError { get; }
}
