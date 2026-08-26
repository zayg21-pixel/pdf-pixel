using System;

namespace PdfPixel.Models;

/// <summary>
/// Execution parameters for <see cref="Commands.Context.PdfCommandExecutionContext"/>.
/// Contains <see cref="Commands.Model.IPdfCommand"/> specific parameters that can change between command execution.
/// </summary>
public class PdfCommandExecutionParameters : IEquatable<PdfCommandExecutionParameters>, ICloneable
{
    /// <summary>
    /// If true - antialiazing will be enabled for rendering useful for CPU rendering to avoid jagged edges.
    /// </summary>
    public bool Antialias { get; set; } = true;

    /// <summary>
    /// Standard image tile size.
    /// </summary>
    public int ImageTileSize { get; set; } = 1024;

    /// <summary>
    /// If true - rects and image tiles are snapped to whole device pixels.
    /// </summary>
    public bool SnapToDevicePixels { get; set; } = true;

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
            && SnapToDevicePixels == other.SnapToDevicePixels;
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfCommandExecutionParameters);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Antialias,
            ImageTileSize,
            SnapToDevicePixels);
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
            ImageTileSize = ImageTileSize,
            SnapToDevicePixels = SnapToDevicePixels
        };
    }

    object ICloneable.Clone() => Clone();
}
