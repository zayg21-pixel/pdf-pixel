using PdfReader.Models;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Rendering
{
    /// <summary>
    /// Enumeration of supported soft mask subtypes.
    /// </summary>
    [PdfEnum]
    public enum PdfSoftMaskSubtype
    {
        /// <summary>
        /// Unknown soft mask subtype (default).
        /// </summary>
        [PdfEnumDefaultValue]
        Unknown = 0,

        /// <summary>
        /// Alpha soft mask subtype (/Alpha).
        /// </summary>
        [PdfEnumValue("Alpha")]
        Alpha,

        /// <summary>
        /// Luminosity soft mask subtype (/Luminosity).
        /// </summary>
        [PdfEnumValue("Luminosity")]
        Luminosity
    }

    /// <summary>
    /// Represents a PDF soft mask for advanced transparency effects.
    /// Soft masks are commonly used for drop shadows, glows, and other complex transparency effects.
    /// </summary>
    public class PdfSoftMask
    {
        /// <summary>
        /// Parsed soft mask subtype (/S). Defaults to Unknown when not /Alpha or /Luminosity.
        /// </summary>
        public PdfSoftMaskSubtype Subtype { get; set; } = PdfSoftMaskSubtype.Unknown;

        /// <summary>
        /// Reference to the Form XObject that defines the mask (/G entry).
        /// </summary>
        public PdfObject GroupObject { get; set; }

        /// <summary>
        /// Parsed transparency group dictionary (/Group of the Form XObject) if present.
        /// May be null when the mask Form does not specify a transparency group.
        /// </summary>
        public PdfTransparencyGroup TransparencyGroup { get; set; }

        /// <summary>
        /// Background color (BC) resolved to sRGB using the soft mask group's color space (/Group /CS).
        /// Null when not specified.
        /// </summary>
        public SKColor? BackgroundColor { get; set; }

        /// <summary>
        /// Transfer function (TR) - function or name for color transformation.
        /// </summary>
        public IPdfValue TransferFunction { get; set; }

        /// <summary>
        /// /Matrix of the soft mask Form XObject. Identity when not specified.
        /// </summary>
        public SKMatrix FormMatrix { get; set; } = SKMatrix.Identity;

        /// <summary>
        /// The untransformed /BBox rectangle of the soft mask Form XObject. Empty when not specified.
        /// </summary>
        public SKRect BBox { get; set; }

        /// <summary>
        /// The /BBox transformed by FormMatrix (if any). Equals BBox when no matrix was supplied.
        /// Empty when BBox not specified.
        /// </summary>
        public SKRect TransformedBounds { get; set; } // TODO: we might want to remove this

        /// <summary>
        /// Cached /Resources dictionary of the soft mask Form XObject (may be null).
        /// </summary>
        public PdfDictionary ResourcesDictionary { get; set; }
    }
}