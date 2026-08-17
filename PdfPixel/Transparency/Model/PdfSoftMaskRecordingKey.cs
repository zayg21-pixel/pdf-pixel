using PdfPixel.Geometry;
using PdfPixel.Models;
using System;

namespace PdfPixel.Transparency.Model;

/// <summary>
/// Identifies one recording of a soft mask form's content.
/// </summary>
internal readonly struct PdfSoftMaskRecordingKey : IEquatable<PdfSoftMaskRecordingKey>
{
    /// <summary>
    /// Initializes the key with the mask form object and the matrix its content was recorded under.
    /// </summary>
    public PdfSoftMaskRecordingKey(in PdfReference maskForm, in PdfMatrix worldToMaskForm)
    {
        MaskForm = maskForm;
        WorldToMaskForm = worldToMaskForm;
    }

    /// <summary>
    /// Gets the reference of the mask form object whose content was recorded.
    /// </summary>
    public PdfReference MaskForm { get; }

    /// <summary>
    /// Gets the matrix mapping the space the mask is used from onto the mask form's own space.
    /// </summary>
    public PdfMatrix WorldToMaskForm { get; }

    /// <inheritdoc />
    public bool Equals(PdfSoftMaskRecordingKey other)
        => MaskForm == other.MaskForm && WorldToMaskForm == other.WorldToMaskForm;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfSoftMaskRecordingKey other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(MaskForm, WorldToMaskForm);

    /// <summary>
    /// Returns whether the two keys name the same recording.
    /// </summary>
    public static bool operator ==(in PdfSoftMaskRecordingKey left, in PdfSoftMaskRecordingKey right) => left.Equals(right);

    /// <summary>
    /// Returns whether the two keys name different recordings.
    /// </summary>
    public static bool operator !=(in PdfSoftMaskRecordingKey left, in PdfSoftMaskRecordingKey right) => !left.Equals(right);
}
