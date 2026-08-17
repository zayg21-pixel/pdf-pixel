using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Forms;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Represents a PDF soft mask for advanced transparency effects.
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
    public PdfForm? MaskForm { get; set; }

    /// <summary>
    /// Background color (BC) components.
    /// </summary>
    public float[]? BackgroundColorComponents { get; set; }

    /// <summary>
    /// Optional transfer function transform (TR) applied to device output before using as soft mask input.
    /// Internal to avoid exposing internal transform type on a public API.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; set; }

    /// <summary>
    /// Retrieves the background color, converting it to sRGB if necessary.
    /// </summary>
    /// <param name="intent">Current intent.</param>
    /// <param name="postTransform">Post color transform (if defined).</param>
    /// <returns>Background color.</returns>
    public PdfColor GetBackgroundColor(PdfRenderingIntent intent, TransferFunctionTransform? postTransform)
    {
        if (MaskForm == null || BackgroundColorComponents == null)
        {
            return PdfColors.Black;
        }

        return MaskForm.TransparencyGroup?.ColorSpaceConverter?.ToSrgb(BackgroundColorComponents, intent, postTransform) ?? PdfColors.Black;
    }
}
