using PdfPixel.Color.Transform;
using PdfPixel.Transparency;

namespace PdfPixel.Color.Paint;

/// <summary>
/// Parameters for a mask paint: the layer paint that composites a previously-rendered mask layer onto content, keeping
/// the content only as much as the mask allows. <see cref="Kind"/> tells what the layer holds; the mask subtype decides
/// whether its rendered color is converted from luminosity, and the optional transfer function (TR) remaps the resulting
/// mask value before it's used as alpha. A <see cref="PdfPaint"/> carries this only when it composites a mask layer; null
/// on every other paint.
/// </summary>
public sealed class PdfMaskPaintParameters
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMaskPaintParameters"/> class.
    /// </summary>
    /// <param name="kind">What the mask layer holds.</param>
    /// <param name="subtype">The soft mask subtype (/S): Alpha or Luminosity.</param>
    /// <param name="transferFunction">The soft mask's transfer function (/TR), or null when absent/identity.</param>
    internal PdfMaskPaintParameters(PdfMaskPaintKind kind, PdfSoftMaskSubtype subtype, TransferFunctionTransform? transferFunction)
    {
        Kind = kind;
        Subtype = subtype;
        TransferFunction = transferFunction;
    }

    /// <summary>
    /// Gets what the mask layer holds: a soft mask, or the shape a pattern is painted within.
    /// </summary>
    public PdfMaskPaintKind Kind { get; }

    /// <summary>
    /// Gets the soft mask subtype (/S). Always <see cref="PdfSoftMaskSubtype.Alpha"/> for a <see cref="PdfMaskPaintKind.Shape"/> mask.
    /// </summary>
    public PdfSoftMaskSubtype Subtype { get; }

    /// <summary>
    /// Gets the soft mask's transfer function (/TR), or null when absent/identity. Always null for a
    /// <see cref="PdfMaskPaintKind.Shape"/> mask.
    /// </summary>
    internal TransferFunctionTransform? TransferFunction { get; }
}
