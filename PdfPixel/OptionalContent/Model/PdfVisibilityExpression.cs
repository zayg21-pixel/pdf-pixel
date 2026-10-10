using PdfPixel.Models;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.OptionalContent.Model;

/// <summary>
/// Represents a parsed /VE visibility expression: a boolean tree combining optional content
/// group references with And/Or/Not operators, as used by an optional content membership
/// dictionary's (OCMD) /VE entry.
/// </summary>
public sealed class PdfVisibilityExpression
{
    private const int MaxNestingDepth = 10;

    private PdfVisibilityExpression(in PdfReference groupReference)
    {
        Type = PdfVisibilityExpressionType.Group;
        GroupReference = groupReference;
        Operands = Array.Empty<PdfVisibilityExpression>();
    }

    private PdfVisibilityExpression(PdfVisibilityExpressionOperator operatorKind, IReadOnlyList<PdfVisibilityExpression> operands)
    {
        Type = PdfVisibilityExpressionType.Operator;
        Operator = operatorKind;
        Operands = operands;
    }

    /// <summary>
    /// Distinguishes whether this node is a <see cref="GroupReference"/> leaf or an <see cref="Operator"/> node.
    /// </summary>
    public PdfVisibilityExpressionType Type { get; }

    /// <summary>
    /// The referenced optional content group, or <see langword="null"/> when <see cref="Type"/> is <see cref="PdfVisibilityExpressionType.Operator"/>.
    /// </summary>
    public PdfReference? GroupReference { get; }

    /// <summary>
    /// The operator combining <see cref="Operands"/>, or <see langword="null"/> when <see cref="Type"/> is <see cref="PdfVisibilityExpressionType.Group"/>.
    /// </summary>
    public PdfVisibilityExpressionOperator? Operator { get; }

    /// <summary>
    /// Operands combined by <see cref="Operator"/>. Empty when <see cref="Type"/> is <see cref="PdfVisibilityExpressionType.Group"/>.
    /// </summary>
    public IReadOnlyList<PdfVisibilityExpression> Operands { get; }

    /// <summary>
    /// Parses a /VE array into an expression tree. Returns <see langword="null"/> for a
    /// malformed expression (fewer than 2 elements) or one nested deeper than
    /// <see cref="MaxNestingDepth"/>.
    /// </summary>
    internal static PdfVisibilityExpression? Parse(PdfArray array) => Parse(array, 0);

    private static PdfVisibilityExpression? Parse(PdfArray array, int nestingDepth)
    {
        if (nestingDepth >= MaxNestingDepth || array.Count < 2)
        {
            return null;
        }

        PdfVisibilityExpressionOperator operatorKind = array.GetNameOrDefault(0).AsEnum<PdfVisibilityExpressionOperator>();

        List<PdfVisibilityExpression> operands = new(array.Count - 1);
        for (int index = 1; index < array.Count; index++)
        {
            PdfArray? nestedArray = array.GetArray(index);
            if (nestedArray != null)
            {
                PdfVisibilityExpression? nestedExpression = Parse(nestedArray, nestingDepth + 1);
                if (nestedExpression != null)
                {
                    operands.Add(nestedExpression);
                }

                continue;
            }

            PdfReference? groupReference = array.GetReference(index);
            if (groupReference != null && groupReference.Value.IsValid)
            {
                operands.Add(new PdfVisibilityExpression(groupReference.Value));
            }
        }

        return new PdfVisibilityExpression(operatorKind, operands);
    }
}
