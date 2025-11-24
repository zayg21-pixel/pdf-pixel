using SkiaSharp;
using System;

namespace PdfReader.Imaging.Decoding;

/// <summary>
/// Represents the result of decoding a PDF image, including the decoded image and flags indicating which post-processing steps have been applied.
/// </summary>
public class PdfImageDecodingResult : IDisposable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfImageDecodingResult"/> class with the specified decoded image.
    /// </summary>
    /// <param name="image">The decoded <see cref="SKImage"/>. Must not be null.</param>
    /// <exception cref="ArgumentNullException">Thrown if <paramref name="image"/> is null.</exception>
    public PdfImageDecodingResult(SKImage image)
    {
        Image = image ?? throw new ArgumentNullException(nameof(image));
    }

    /// <summary>
    /// Gets the decoded image.
    /// </summary>
    public SKImage Image { get; }

    /// <summary>
    /// Gets or sets a value indicating whether the /Decode array has been applied to the image.
    /// </summary>
    public bool DecodeApplied { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the /MaskArray (color key mask) has been removed from the image.
    /// Does not refer to soft masks.
    /// </summary>
    public bool MaskRemoved { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether color space conversion has been performed on the image.
    /// </summary>
    public bool ColorConverted { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the alpha channel has been set (e.g., from a mask or soft mask).
    /// </summary>
    public bool AlphaSet { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether matte color removal (dematting) has been performed.
    /// </summary>
    public bool MatteRemoved { get; set; }

    /// <summary>
    /// Disposes the underlying <see cref="SKImage"/> resource.
    /// </summary>
    public void Dispose()
    {
        Image.Dispose();
    }
}
