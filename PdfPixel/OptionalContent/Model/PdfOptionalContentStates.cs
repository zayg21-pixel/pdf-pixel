using PdfPixel.Models;
using System;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Snapshot of the ON/OFF state of optional content groups, keyed by group reference.
/// </summary>
public sealed class PdfOptionalContentStates : IEquatable<PdfOptionalContentStates>
{
    private readonly IReadOnlyDictionary<PdfReference, bool> _states;

    internal PdfOptionalContentStates(IReadOnlyDictionary<PdfReference, bool> states) => _states = states;

    /// <summary>
    /// Returns whether <paramref name="group"/> is ON, or <see langword="null"/> when the snapshot does not hold the group.
    /// </summary>
    /// <param name="group">Reference of the optional content group.</param>
    public bool? GetState(in PdfReference group)
    {
        if (_states.TryGetValue(group, out bool isOn))
        {
            return isOn;
        }

        return null;
    }

    /// <inheritdoc/>
    public bool Equals(PdfOptionalContentStates? other)
    {
        if (ReferenceEquals(this, other))
        {
            return true;
        }

        if (ReferenceEquals(other, null) || _states.Count != other._states.Count)
        {
            return false;
        }

        foreach (KeyValuePair<PdfReference, bool> entry in _states)
        {
            if (!other._states.TryGetValue(entry.Key, out bool isOn) || isOn != entry.Value)
            {
                return false;
            }
        }

        return true;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => Equals(obj as PdfOptionalContentStates);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        int hash = 0;

        foreach (KeyValuePair<PdfReference, bool> entry in _states)
        {
            hash += HashCode.Combine(entry.Key, entry.Value);
        }

        return hash;
    }

    /// <summary>
    /// Determines whether two snapshots hold the same state for every group.
    /// </summary>
    public static bool operator ==(PdfOptionalContentStates? left, PdfOptionalContentStates? right)
    {
        if (ReferenceEquals(left, null))
        {
            return ReferenceEquals(right, null);
        }

        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two snapshots differ in the state of a group.
    /// </summary>
    public static bool operator !=(PdfOptionalContentStates? left, PdfOptionalContentStates? right) => !(left == right);
}
