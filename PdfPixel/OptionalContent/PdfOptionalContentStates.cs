using PdfPixel.Objects;
using System;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent;

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

    /// <summary>
    /// Returns whether content with the optional content membership <paramref name="membership"/> is visible under this snapshot.
    /// </summary>
    /// <param name="membership">Optional content membership of the content.</param>
    public bool IsVisible(PdfOptionalContentMembership membership)
    {
        if (membership == null)
        {
            throw new ArgumentNullException(nameof(membership));
        }

        if (membership.VisibilityExpression != null)
        {
            return EvaluateVisibilityExpression(membership.VisibilityExpression);
        }

        int onCount = 0;
        int offCount = 0;

        foreach (PdfReference group in membership.Groups)
        {
            bool? state = GetState(group);

            if (state == null)
            {
                continue;
            }

            if (state.Value)
            {
                onCount++;
            }
            else
            {
                offCount++;
            }
        }

        if (onCount + offCount == 0)
        {
            return true;
        }

        return membership.VisibilityPolicy switch
        {
            PdfOptionalContentVisibilityPolicy.AllOn => offCount == 0,
            PdfOptionalContentVisibilityPolicy.AnyOn => onCount > 0,
            PdfOptionalContentVisibilityPolicy.AnyOff => offCount > 0,
            PdfOptionalContentVisibilityPolicy.AllOff => onCount == 0,
            _ => true
        };
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

    private bool EvaluateVisibilityExpression(PdfVisibilityExpression expression)
    {
        if (expression.GroupReference != null)
        {
            return GetGroupVisibility(expression.GroupReference.Value);
        }

        IReadOnlyList<PdfVisibilityExpression> operands = expression.Operands;

        switch (expression.Operator)
        {
            case PdfVisibilityExpressionOperator.And:
            {
                foreach (PdfVisibilityExpression operand in operands)
                {
                    if (!EvaluateVisibilityExpression(operand))
                    {
                        return false;
                    }
                }

                return true;
            }
            case PdfVisibilityExpressionOperator.Or:
            {
                foreach (PdfVisibilityExpression operand in operands)
                {
                    if (EvaluateVisibilityExpression(operand))
                    {
                        return true;
                    }
                }

                return false;
            }
            case PdfVisibilityExpressionOperator.Not:
            {
                return operands.Count > 0 && !EvaluateVisibilityExpression(operands[0]);
            }
            default:
            {
                return true;
            }
        }
    }

    private bool GetGroupVisibility(in PdfReference reference)
    {
        bool? state = GetState(reference);

        if (state == null)
        {
            return true;
        }

        return state.Value;
    }
}
