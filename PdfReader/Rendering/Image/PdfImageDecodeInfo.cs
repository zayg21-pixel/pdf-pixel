using SkiaSharp;

namespace PdfReader.Rendering.Image
{
    /// <summary>
    /// Result of decoding a PdfImage.
    /// Contains the produced SKImage and diagnostic flags about internal processing.
    /// </summary>
    public sealed class PdfImageDecodeResult
    {
        /// <summary>
        /// The decoded image, or null if decoding failed or format unsupported.
        /// </summary>
        public SKImage Image { get; internal set; }

        /// <summary>
        /// Convenience flag: true if Image is not null.
        /// </summary>
        public bool IsConverted { get; internal set; }

        /// <summary>
        /// True if a PNG/TIFF predictor was undone according to /DecodeParms.
        /// </summary>
        public bool PredictorUndone { get; internal set; }

        /// <summary>
        /// True if the PDF /Decode array was applied to samples inside the decoder.
        /// </summary>
        public bool AppliedDecode { get; internal set; }

        /// <summary>
        /// True if the decoder applied color-space conversion to sRGB (e.g., ICCBased/Cal*).
        /// </summary>
        public bool AppliedColorConversion { get; internal set; }

        /// <summary>
        /// True when the decoded image represents an ImageMask (stencil) and is alpha-only.
        /// The drawer should paint using the current non-stroking color through this mask.
        /// </summary>
        public bool IsImageMask { get; internal set; }
    }
}
