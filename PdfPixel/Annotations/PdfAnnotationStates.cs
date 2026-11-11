using System;
using System.Collections.Generic;

namespace PdfPixel.Annotations;

/// <summary>
/// The states annotations are drawn in, by annotation; an annotation without one is drawn in
/// <see cref="PdfAnnotationState.Default"/>.
/// </summary>
public sealed class PdfAnnotationStates : IEquatable<PdfAnnotationStates>
{
    private readonly Dictionary<PdfAnnotation, PdfAnnotationState> _states;

    /// <summary>
    /// Initializes the states from <paramref name="states"/>, which they keep and read from.
    /// </summary>
    /// <param name="states">The state of each annotation that is not drawn in the default state.</param>
    public PdfAnnotationStates(Dictionary<PdfAnnotation, PdfAnnotationState> states) => _states = states ?? throw new ArgumentNullException(nameof(states));

    /// <summary>
    /// Gets the states that draw every annotation in <see cref="PdfAnnotationState.Default"/>.
    /// </summary>
    public static PdfAnnotationStates Empty { get; } = new([]);

    /// <summary>
    /// Returns the state <paramref name="annotation"/> is drawn in, <see cref="PdfAnnotationState.Default"/> when it
    /// has none.
    /// </summary>
    public PdfAnnotationState GetState(PdfAnnotation annotation)
    {
        if (annotation == null)
        {
            throw new ArgumentNullException(nameof(annotation));
        }

        if (_states.TryGetValue(annotation, out PdfAnnotationState? state))
        {
            return state;
        }
        else
        {
            return PdfAnnotationState.Default;
        }
    }

    /// <inheritdoc/>
    public bool Equals(PdfAnnotationStates? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (ReferenceEquals(other, null) || _states.Count != other._states.Count)
        {
            return false;
        }

        foreach (KeyValuePair<PdfAnnotation, PdfAnnotationState> entry in _states)
        {
            if (!other._states.TryGetValue(entry.Key, out PdfAnnotationState? state) || state != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PdfAnnotationStates);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;

        foreach (KeyValuePair<PdfAnnotation, PdfAnnotationState> entry in _states)
        {
            hash += HashCode.Combine(entry.Key, entry.Value);
        }

        return hash;
    }

    /// <summary>
    /// Determines whether two sets of states draw every annotation the same way.
    /// </summary>
    public static bool operator ==(PdfAnnotationStates? left, PdfAnnotationStates? right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two sets of states draw an annotation differently.
    /// </summary>
    public static bool operator !=(PdfAnnotationStates? left, PdfAnnotationStates? right) => !(left == right);
}
