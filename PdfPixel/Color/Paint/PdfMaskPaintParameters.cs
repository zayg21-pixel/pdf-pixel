using PdfPixel.Color.Transform;
using PdfPixel.Transparency.Model;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Parameters for a soft-mask compositing paint: the mask subtype (which decides whether the mask's
/// rendered color is converted from luminosity) and the optional transfer function (TR) that remaps the
/// resulting mask value before it's used as alpha. A <see cref="PdfPaint"/> carries this only when it
/// composites a previously-rendered soft-mask layer onto content; null on every other paint.
/// </summary>
public sealed class PdfMaskPaintParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMaskPaintParameters"/> class.
    /// </summary>
    /// <param name="subtype">The soft mask subtype (/S): Alpha or Luminosity.</param>
    /// <param name="transferFunction">The soft mask's transfer function (/TR), or null when absent/identity.</param>
    public PdfMaskPaintParameters(PdfSoftMaskSubtype subtype, TransferFunctionTransform? transferFunction)
    {
        Subtype = subtype;
        TransferFunction = transferFunction;
    }

    /// <summary>
    /// Gets the soft mask subtype (/S).
    /// </summary>
    public PdfSoftMaskSubtype Subtype { get; }

    /// <summary>
    /// Gets the soft mask's transfer function (/TR), or null when absent/identity.
    /// </summary>
    public TransferFunctionTransform? TransferFunction { get; }
}
