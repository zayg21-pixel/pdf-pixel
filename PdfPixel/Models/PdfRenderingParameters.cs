using SkiaSharp;
using System;

namespace PdfPixel.Models;

/// <summary>
/// Rendering parameters for <see cref="PdfPage"/>.
/// </summary>
public class PdfRenderingParameters : IEquatable<PdfRenderingParameters>, ICloneable
{
    /// <summary>
    /// Indicates whether the rendering is intended for printing.
    /// When true, affects annotation visibility (only annotations with Print flag are rendered).
    /// </summary>
    public bool PrintMode { get; set; }

    /// <summary>
    /// If true - antialiazing will be enabled for rendering useful for CPU rendering to avoid jagged edges.
    /// </summary>
    public bool Antialias { get; set; } = true;

    /// <summary>
    /// Actual device scale factor, if defined, all images will be downscaled
    /// to fit exact device scale, otherwise decoded in full size.
    /// </summary>
    public float? ScaleFactor { get; set; }

    /// <summary>
    /// Default number of samples for Postscript and Exponential functions when the point count is unknown.
    /// </summary>
    public int DefaultFunctionSamples { get; set; } = 64;

    /// <summary>
    /// Maximum number of tessellation vertices for mesh-based shadings.
    /// </summary>
    public int MaxTessellationVertices { get; set; } = 32;

    /// <summary>
    /// Standard image tile size.
    /// </summary>
    public int ImageTileSize { get; set; } = 1024;

    /// <inheritdoc />
    public bool Equals(PdfRenderingParameters other)
    {
        if (other == null)
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return PrintMode == other.PrintMode
            && Antialias == other.Antialias
            && ScaleFactor == other.ScaleFactor
            && DefaultFunctionSamples == other.DefaultFunctionSamples
            && MaxTessellationVertices == other.MaxTessellationVertices;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => Equals(obj as PdfRenderingParameters);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            PrintMode,
            Antialias,
            ScaleFactor,
            DefaultFunctionSamples,
            MaxTessellationVertices,
            ImageTileSize);
    }

    /// <summary>
    /// Determines whether two <see cref="PdfRenderingParameters"/> instances are equal.
    /// </summary>
    public static bool operator ==(PdfRenderingParameters left, PdfRenderingParameters right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.Equals(right) ?? false;
    }

    /// <summary>
    /// Determines whether two <see cref="PdfRenderingParameters"/> instances are not equal.
    /// </summary>
    public static bool operator !=(PdfRenderingParameters left, PdfRenderingParameters right) => !(left == right);

    public PdfRenderingParameters Clone()
    {
        return new()
        {
            PrintMode = PrintMode,
            Antialias = Antialias,
            ScaleFactor = ScaleFactor,
            DefaultFunctionSamples = DefaultFunctionSamples,
            MaxTessellationVertices = MaxTessellationVertices,
            ImageTileSize = ImageTileSize
        };
    }

    object ICloneable.Clone() => Clone();
}
