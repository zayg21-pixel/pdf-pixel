using PdfPixel.Color.Transform;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;

namespace PdfPixel.Color.ColorSpace;

/// <summary>
/// Base type for PDF color space converters producing sRGB output.
/// </summary>
public abstract class PdfColorSpaceConverter
{
    /// <summary>
    /// Gets the number of input components for the color space (e.g. 1=Gray, 3=RGB, 4=CMYK).
    /// </summary>
    public abstract int Components { get; }

    /// <summary>
    /// Gets a value indicating whether this converter represents a device (native) color space.
    /// </summary>
    public abstract bool IsDevice { get; }

    /// <summary>
    /// Optional transform mapping this space's real component values into the ICC profile connection
    /// space encoding (0..1). Null when component values are already in that encoding, which covers
    /// most color spaces. Used by <see cref="PdfIccColorSpaceConverter"/> to correctly feed real component
    /// values of its Alternate into an embedded ICC profile transform.
    /// </summary>
    public virtual IColorTransform? NormalizeTransform => null;

    /// <summary>
    /// Optional transform mapping the ICC profile connection space encoding (0..1) back to this space's real
    /// component values: the inverse of <see cref="NormalizeTransform"/>, null when that is null.
    /// </summary>
    public virtual IColorTransform? DenormalizeTransform => null;

    /// <summary>
    /// Gets the range of each component of the color space, or null when every component spans [0 1].
    /// </summary>
    public virtual PdfRange[]? ComponentRanges => null;

    /// <summary>
    /// Returns the component values of the initial color the CS and cs operators set for this color space.
    /// </summary>
    internal virtual float[] GetInitialComponents() => new float[Components];

    /// <summary>
    /// Returns the transform taking this space's component values to the profile connection space (CIE XYZ D50),
    /// or null when the space converts no colors itself.
    /// </summary>
    /// <param name="renderingIntent">Rendering intent the colors are converted with.</param>
    /// <param name="useBlackPointCompensation">Whether black point compensation is applied.</param>
    public abstract ChainedColorTransform? GetProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation);

    /// <summary>
    /// Returns the transform taking the profile connection space (CIE XYZ D50) to this space's component values, or
    /// null when the space converts no colors itself.
    /// </summary>
    /// <param name="renderingIntent">Rendering intent the colors are converted with.</param>
    /// <param name="useBlackPointCompensation">Whether black point compensation is applied.</param>
    public abstract ChainedColorTransform? GetFromProfileConnectionTransform(PdfRenderingIntent renderingIntent, bool useBlackPointCompensation);
}
