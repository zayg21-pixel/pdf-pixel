using PdfReader.Color.ColorSpace;
using PdfReader.Forms;
using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Transparency.Model;

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
    public PdfForm MaskForm { get; set; }

    /// <summary>
    /// Background color (BC) components.
    /// </summary>
    public float[] BackgroundColor { get; set; }

    /// <summary>
    /// Retrieves the background color as an SKColor, converting it to sRGB if necessary.
    /// </summary>
    /// <param name="intent">Current intent.</param>
    /// <returns>SKColor instance.</returns>
    public SKColor GetBackgroundColor(PdfRenderingIntent intent)
    {
        SKColor backgroundColor;

        if (BackgroundColor != null)
        {
            backgroundColor = MaskForm.TransparencyGroup?.ColorSpaceConverter?.ToSrgb(BackgroundColor, intent) ?? SKColors.White;
        }
        else
        {
            backgroundColor = SKColors.White;
        }

        return backgroundColor;
    }
}