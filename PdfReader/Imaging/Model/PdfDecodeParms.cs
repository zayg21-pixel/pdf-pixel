using PdfReader.Models;
using PdfReader.Text;

namespace PdfReader.Imaging.Model;

/// <summary>
/// Parsed /DecodeParms for an image stream.
/// Contains optional parameters used by specific filters and predictors.
/// Values are read-only from outside and are populated by <see cref="FromDictionary(PdfDictionary)"/>.
/// </summary>
public class PdfDecodeParameters
{
    /// <summary>
    /// PNG/TIFF predictor method used for post-decompression sample reconstruction.
    /// 2 = TIFF predictor; 10–15 = PNG predictors.
    /// Applies to FlateDecode and LZWDecode.
    /// </summary>
    public int? Predictor { get; private set; }

    /// <summary>
    /// Number of interleaved color components used by the predictor (not necessarily the color space components).
    /// </summary>
    public int? Colors { get; private set; }

    /// <summary>
    /// Bits per component expected by the predictor (can differ from /BitsPerComponent in some edge cases).
    /// </summary>
    public int? BitsPerComponent { get; private set; }

    /// <summary>
    /// Number of columns (pixels per row) used by the predictor algorithms.
    /// </summary>
    public int? Columns { get; private set; }

    /// <summary>
    /// CCITT parameter K indicating the compression scheme: 0 = Group 3 1-D, &gt;0 = Group 3 2-D, &lt;0 = Group 4 2-D.
    /// </summary>
    public int? K { get; private set; }

    /// <summary>
    /// If true, each row ends with an end-of-line (EOL) marker (CCITT).
    /// </summary>
    public bool? EndOfLine { get; private set; }

    /// <summary>
    /// If true, end-of-line is byte-aligned (CCITT).
    /// </summary>
    public bool? EncodedByteAlign { get; private set; }

    /// <summary>
    /// Number of rows in the image (CCITT). If absent, defaults to /Height.
    /// </summary>
    public int? Rows { get; private set; }

    /// <summary>
    /// If true, a special end-of-block (EOFB) marker is present (CCITT).
    /// </summary>
    public bool? EndOfBlock { get; private set; }

    /// <summary>
    /// If true, black pixels are represented by 1 and white by 0 (CCITT). Default is false.
    /// </summary>
    public bool? BlackIs1 { get; private set; }

    /// <summary>
    /// Maximum number of damaged rows tolerated before an error is reported (CCITT).
    /// </summary>
    public int? DamagedRowsBeforeError { get; private set; }

    /// <summary>
    /// LZW parameter controlling early code size change (usually 1).
    /// </summary>
    public int? EarlyChange { get; private set; }

    /// <summary>
    /// JPEG (DCTDecode) parameter indicating color transform (e.g., 1 for YCbCr to RGB).
    /// </summary>
    public int? ColorTransform { get; private set; }

    /// <summary>
    /// Parse a /DecodeParms dictionary to a strongly-typed <see cref="PdfDecodeParameters"/> instance.
    /// Unrecognized keys are ignored.
    /// </summary>
    public static PdfDecodeParameters FromDictionary(PdfDictionary dictionary)
    {
        var parameters = new PdfDecodeParameters();
        if (dictionary == null)
        {
            return parameters;
        }

        // Predictor related
        parameters.Predictor = dictionary.GetInteger(PdfTokens.PredictorKey);
        parameters.Colors = dictionary.GetInteger(PdfTokens.ColorsKey);
        parameters.BitsPerComponent = dictionary.GetInteger(PdfTokens.BitsPerComponentKey);
        parameters.Columns = dictionary.GetInteger(PdfTokens.ColumnsKey);

        // CCITT related
        parameters.K = dictionary.GetInteger(PdfTokens.KKey);
        parameters.EndOfLine = dictionary.GetBool(PdfTokens.EndOfLineKey);
        parameters.EncodedByteAlign = dictionary.GetBool(PdfTokens.EncodedByteAlignKey);
        parameters.Rows = dictionary.GetInteger(PdfTokens.RowsKey);
        parameters.EndOfBlock = dictionary.GetBool(PdfTokens.EndOfBlockKey);
        parameters.BlackIs1 = dictionary.GetBool(PdfTokens.BlackIs1Key);
        parameters.DamagedRowsBeforeError = dictionary.GetInteger(PdfTokens.DamagedRowsBeforeErrorKey);

        // LZW
        parameters.EarlyChange = dictionary.GetInteger(PdfTokens.EarlyChangeKey);

        // DCT
        parameters.ColorTransform = dictionary.GetInteger(PdfTokens.ColorTransformKey);

        return parameters;
    }
}
