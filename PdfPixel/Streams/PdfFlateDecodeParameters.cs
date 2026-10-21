using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Streams;

/// <summary>
/// Parsed /DecodeParms entry of a FlateDecode filter: the predictor applied after decompression.
/// </summary>
public class PdfFlateDecodeParameters : PdfDecodeParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFlateDecodeParameters"/> class from a /DecodeParms dictionary.
    /// </summary>
    /// <param name="dictionary">The /DecodeParms dictionary.</param>
    internal PdfFlateDecodeParameters(PdfDictionary dictionary)
    {
        Predictor = dictionary.GetInteger(PdfTokens.PredictorKey);
        Colors = dictionary.GetInteger(PdfTokens.ColorsKey);
        BitsPerComponent = dictionary.GetInteger(PdfTokens.BitsPerComponentKey);
        Columns = dictionary.GetInteger(PdfTokens.ColumnsKey);
    }

    /// <summary>
    /// PNG/TIFF predictor method used for post-decompression sample reconstruction (/Predictor):
    /// 2 = TIFF predictor; 10–15 = PNG predictors. Null when absent.
    /// </summary>
    public int? Predictor { get; }

    /// <summary>
    /// Number of interleaved color components used by the predictor (/Colors), or null when absent.
    /// </summary>
    public int? Colors { get; }

    /// <summary>
    /// Bits per component expected by the predictor (/BitsPerComponent), or null when absent.
    /// </summary>
    public int? BitsPerComponent { get; }

    /// <summary>
    /// Number of samples in each row used by the predictor (/Columns), or null when absent.
    /// </summary>
    public int? Columns { get; }
}
