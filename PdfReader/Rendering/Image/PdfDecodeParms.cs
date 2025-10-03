using PdfReader.Models;
using System;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Parsed /DecodeParms for an image stream.
    /// Contains optional parameters used by specific filters and predictors.
    /// Values are read-only from outside and are populated by <see cref="FromDictionary(PdfReader.Models.PdfDictionary)"/>.
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
            if (dictionary == null) return parameters;

            // Predictor
            if (dictionary.TryGetInt(PdfTokens.PredictorKey, out var predictor)) parameters.Predictor = predictor;
            if (dictionary.TryGetInt(PdfTokens.ColorsKey, out var colors)) parameters.Colors = colors;
            if (dictionary.TryGetInt(PdfTokens.BitsPerComponentKey, out var bitsPerComponent)) parameters.BitsPerComponent = bitsPerComponent;
            if (dictionary.TryGetInt(PdfTokens.ColumnsKey, out var columns)) parameters.Columns = columns;

            // CCITT
            if (dictionary.TryGetInt(PdfTokens.KKey, out var k)) parameters.K = k;
            if (dictionary.TryGetBool(PdfTokens.EndOfLineKey, out var endOfLine)) parameters.EndOfLine = endOfLine;
            if (dictionary.TryGetBool(PdfTokens.EncodedByteAlignKey, out var encodedByteAlign)) parameters.EncodedByteAlign = encodedByteAlign;
            if (dictionary.TryGetInt(PdfTokens.RowsKey, out var rows)) parameters.Rows = rows;
            if (dictionary.TryGetBool(PdfTokens.EndOfBlockKey, out var endOfBlock)) parameters.EndOfBlock = endOfBlock;
            if (dictionary.TryGetBool(PdfTokens.BlackIs1Key, out var blackIs1)) parameters.BlackIs1 = blackIs1;
            if (dictionary.TryGetInt(PdfTokens.DamagedRowsBeforeErrorKey, out var damagedRowsBeforeError)) parameters.DamagedRowsBeforeError = damagedRowsBeforeError;

            // LZW
            if (dictionary.TryGetInt(PdfTokens.EarlyChangeKey, out var earlyChange)) parameters.EarlyChange = earlyChange;

            // DCT
            if (dictionary.TryGetInt(PdfTokens.ColorTransformKey, out var colorTransform)) parameters.ColorTransform = colorTransform;

            return parameters;
        }
    }
}
