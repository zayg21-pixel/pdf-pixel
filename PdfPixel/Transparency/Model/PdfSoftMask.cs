using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Forms;
using PdfPixel.Rendering.State;

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
    /// <param name="state">Graphics state the mask is applied in.</param>
    /// <returns>Background color.</returns>
    internal PdfColor GetBackgroundColor(PdfGraphicsState state)
    {
        if (MaskForm == null || MaskForm.TransparencyGroup == null || BackgroundColorComponents == null)
        {
            return PdfColors.Black;
        }

        PdfColorSpaceConverter? converter = state.Host.Cache.ColorSpace.Resolve(MaskForm.TransparencyGroup.ColorSpaceReference);

        return converter?.ToSrgb(BackgroundColorComponents, state.RenderingIntent, state.TransferFunction) ?? PdfColors.Black;
    }
}
