using System;

namespace PdfPixel.Models;

/// <summary>
/// Execution parameters for <see cref="Commands.PdfCommandExecutionContext"/>.
/// Contains <see cref="Commands.IPdfCommand"/> specific parameters that can change between command execution.
/// </summary>
public class PdfCommandExecutionParameters : IEquatable<PdfCommandExecutionParameters>, ICloneable
{
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
    public bool Equals(PdfCommandExecutionParameters? other)
    {
        if (ReferenceEquals(null, other))
        {
            return false;
        }

        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return Antialias == other.Antialias
            && ScaleFactor == other.ScaleFactor
            && DefaultFunctionSamples == other.DefaultFunctionSamples
            && MaxTessellationVertices == other.MaxTessellationVertices;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfCommandExecutionParameters);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Antialias,
            ScaleFactor,
            DefaultFunctionSamples,
            MaxTessellationVertices,
            ImageTileSize);
    }

    /// <summary>
    /// Determines whether two <see cref="PdfCommandExecutionParameters"/> instances are equal.
    /// </summary>
    public static bool operator ==(PdfCommandExecutionParameters? left, PdfCommandExecutionParameters? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }

        return left?.Equals(right) ?? false;
    }

    /// <summary>
    /// Determines whether two <see cref="PdfCommandExecutionParameters"/> instances are not equal.
    /// </summary>
    public static bool operator !=(PdfCommandExecutionParameters left, PdfCommandExecutionParameters right) => !(left == right);

    /// <inheritdoc/>
    public PdfCommandExecutionParameters Clone()
    {
        return new()
        {
            Antialias = Antialias,
            ScaleFactor = ScaleFactor,
            DefaultFunctionSamples = DefaultFunctionSamples,
            MaxTessellationVertices = MaxTessellationVertices,
            ImageTileSize = ImageTileSize
        };
    }

    object ICloneable.Clone() => Clone();
}
