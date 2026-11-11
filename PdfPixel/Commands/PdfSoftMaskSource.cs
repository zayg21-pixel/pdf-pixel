using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Rendering;
using PdfPixel.Transparency;
using System;

namespace PdfPixel.Commands;

/// <summary>
/// A soft mask (ISO 32000-2 11.6.5), positioned in the space current when it was set.
/// </summary>
public sealed class PdfSoftMaskSource
{
    internal PdfSoftMaskSource(
        PdfSoftMaskSubtype subtype,
        PdfGroupSource group,
        in PdfRectangle boundingBox,
        PdfPaint? backdropPaint,
        PdfTransferFunction? transferFunction)
    {
        Subtype = subtype;
        Group = group ?? throw new ArgumentNullException(nameof(group));
        BoundingBox = boundingBox;
        BackdropPaint = backdropPaint;
        TransferFunction = transferFunction;
    }

    /// <summary>
    /// Gets whether the mask values are the group's alpha or its luminosity.
    /// </summary>
    public PdfSoftMaskSubtype Subtype { get; }

    /// <summary>
    /// Gets the group whose result gives the mask values.
    /// </summary>
    public PdfGroupSource Group { get; }

    /// <summary>
    /// Gets the group's bounding box, in the space of its recording.
    /// </summary>
    public PdfRectangle BoundingBox { get; }

    /// <summary>
    /// Gets the backdrop the group is composited onto for a luminosity mask, or <see langword="null"/> for an alpha mask.
    /// </summary>
    public PdfPaint? BackdropPaint { get; }

    /// <summary>
    /// Gets the transfer function the mask values are mapped through, or <see langword="null"/> for the identity.
    /// </summary>
    public PdfTransferFunction? TransferFunction { get; }

    /// <inheritdoc />
    public override string ToString() => $"{Subtype} {Group}";
}
