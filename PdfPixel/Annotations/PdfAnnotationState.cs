using PdfPixel.Geometry;
using System;

namespace PdfPixel.Annotations;

/// <summary>
/// The state an annotation is drawn in: its visual state, the matrix it is drawn under and whether it is hidden.
/// </summary>
public sealed class PdfAnnotationState : IEquatable<PdfAnnotationState>
{
    /// <summary>
    /// Initializes a state drawing the annotation in <paramref name="visualState"/> under <paramref name="matrix"/>.
    /// </summary>
    /// <param name="visualState">The visual state whose appearance is drawn.</param>
    /// <param name="matrix">The matrix the appearance is drawn under, in the space of the annotation's page.</param>
    /// <param name="isHidden">Whether the annotation is not drawn at all.</param>
    public PdfAnnotationState(PdfAnnotationVisualStateKind visualState, in PdfMatrix matrix, bool isHidden)
    {
        VisualState = visualState;
        Matrix = matrix;
        IsHidden = isHidden;
    }

    /// <summary>
    /// Gets the state an annotation is drawn in when none is given: normal, under the identity matrix, shown.
    /// </summary>
    public static PdfAnnotationState Default { get; } = new(PdfAnnotationVisualStateKind.Normal, PdfMatrix.Identity, isHidden: false);

    /// <summary>
    /// Gets the visual state whose appearance is drawn.
    /// </summary>
    public PdfAnnotationVisualStateKind VisualState { get; }

    /// <summary>
    /// Gets the matrix the appearance is drawn under, in the space of the annotation's page.
    /// </summary>
    public PdfMatrix Matrix { get; }

    /// <summary>
    /// Gets whether the annotation is not drawn at all.
    /// </summary>
    public bool IsHidden { get; }

    /// <inheritdoc/>
    public bool Equals(PdfAnnotationState? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        return !ReferenceEquals(other, null)
            && VisualState == other.VisualState
            && Matrix == other.Matrix
            && IsHidden == other.IsHidden;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PdfAnnotationState);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(VisualState, Matrix, IsHidden);

    /// <summary>
    /// Determines whether two states draw an annotation the same way.
    /// </summary>
    public static bool operator ==(PdfAnnotationState? left, PdfAnnotationState? right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two states draw an annotation differently.
    /// </summary>
    public static bool operator !=(PdfAnnotationState? left, PdfAnnotationState? right) => !(left == right);
}
