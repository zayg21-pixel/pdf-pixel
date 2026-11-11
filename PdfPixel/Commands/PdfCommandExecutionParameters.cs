using PdfPixel.Color;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Commands;

/// <summary>
/// Execution parameters for <see cref="Execution.PdfCommandExecutionContext"/>.
/// Contains <see cref="IPdfCommand"/> specific parameters that can change between command execution.
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
    /// The color spaces device gray, RGB and CMYK values are converted with.
    /// </summary>
    public PdfDeviceProfiles DeviceProfiles { get; set; } = PdfDeviceProfiles.Default;

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
            && ImageTileSize == other.ImageTileSize
            && ReferenceEquals(DeviceProfiles, other.DeviceProfiles);
    }

    /// <inheritdoc />
    public override bool Equals(object? obj) => Equals(obj as PdfCommandExecutionParameters);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Antialias,
            ImageTileSize,
            RuntimeHelpers.GetHashCode(DeviceProfiles));
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
            DeviceProfiles = DeviceProfiles
        };
    }

    object ICloneable.Clone() => Clone();
}
