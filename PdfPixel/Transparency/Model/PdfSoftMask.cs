using PdfPixel.Color;
using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Transform;
using PdfPixel.Forms;
using PdfPixel.Models;
using PdfPixel.Rendering.State;
using PdfPixel.Text;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Represents a PDF soft mask for advanced transparency effects.
/// </summary>
public class PdfSoftMask
{
    private PdfSoftMask(PdfSoftMaskSubtype subtype, PdfForm maskForm, float[]? backgroundColorComponents, TransferFunctionTransform? transferFunction)
    {
        Subtype = subtype;
        MaskForm = maskForm;
        BackgroundColorComponents = backgroundColorComponents;
        TransferFunction = transferFunction;
    }

    /// <summary>
    /// Parsed soft mask subtype (/S). Defaults to Unknown when not /Alpha or /Luminosity.
    /// </summary>
    public PdfSoftMaskSubtype Subtype { get; }

    /// <summary>
    /// Reference to the Form XObject that defines the mask (/G entry).
    /// </summary>
    public PdfForm MaskForm { get; }

    /// <summary>
    /// Background color (BC) components.
    /// </summary>
    public float[]? BackgroundColorComponents { get; }

    /// <summary>
    /// Optional transfer function transform (TR) applied to device output before using as soft mask input.
    /// Internal to avoid exposing internal transform type on a public API.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }

    /// <summary>
    /// Parses a soft mask dictionary, or returns <see langword="null"/> when it is absent or its /G entry is not a form XObject.
    /// </summary>
    /// <param name="softMaskDictionary">The soft mask dictionary.</param>
    internal static PdfSoftMask? FromDictionary(PdfDictionary? softMaskDictionary)
    {
        if (softMaskDictionary == null)
        {
            return null;
        }

        var maskForm = PdfXObject.GetXObject(softMaskDictionary, PdfTokens.SoftMaskGroupKey) as PdfForm;
        if (maskForm == null)
        {
            return null;
        }

        PdfSoftMaskSubtype subtype = softMaskDictionary.GetNameOrDefault(PdfTokens.SoftMaskSubtypeKey).AsEnum<PdfSoftMaskSubtype>();

        float[]? backgroundColorComponents = null;
        PdfArray? backgroundColorArray = softMaskDictionary.GetArray(PdfTokens.SoftMaskBCKey);
        if (backgroundColorArray?.Count > 0)
        {
            backgroundColorComponents = backgroundColorArray.GetFloatArray();
        }

        TransferFunctionTransform? transferFunction = TransferFunctionTransform.GetTransferFunction(softMaskDictionary, PdfTokens.TransferFunctionKey);

        return new PdfSoftMask(subtype, maskForm, backgroundColorComponents, transferFunction);
    }

    /// <summary>
    /// Retrieves the background color, converting it to sRGB if necessary.
    /// </summary>
    /// <param name="state">Graphics state the mask is applied in.</param>
    /// <returns>Background color.</returns>
    internal PdfColor GetBackgroundColor(PdfGraphicsState state)
    {
        if (MaskForm.TransparencyGroup == null || BackgroundColorComponents == null)
        {
            return PdfColors.Black;
        }

        PdfColorSpaceConverter? converter = state.Host.Cache.ColorSpace.Resolve(MaskForm.TransparencyGroup.ColorSpaceReference);

        return converter?.ToSrgb(BackgroundColorComponents, state.RenderingIntent, state.TransferFunction) ?? PdfColors.Black;
    }
}
