using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a set-OCG-state action that sets the states of optional content groups.
/// </summary>
public class PdfSetOcgStateAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfSetOcgStateAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the set-OCG-state action.</param>
    internal PdfSetOcgStateAction(PdfDictionary actionDictionary)
        : base(PdfActionType.SetOcgState)
    {
        State = ReadState(actionDictionary.GetArray(PdfTokens.OptionalContentStateKey));
        PreserveRadioButtons = actionDictionary.GetBoolean(PdfTokens.PreserveRBKey) ?? true;
    }

    /// <summary>
    /// Groups and the states applied to them (/State), in the order they are applied, or
    /// <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfOptionalContentStateChange>? State { get; }

    /// <summary>
    /// Whether turning a group ON turns OFF the other groups of its radio-button sets (/PreserveRB).
    /// Default value: true.
    /// </summary>
    public bool PreserveRadioButtons { get; }

    /// <summary>
    /// Returns a string representation of this set-OCG-state action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "SetOCGState Action";

    private static List<PdfOptionalContentStateChange>? ReadState(PdfArray? stateArray)
    {
        if (stateArray == null)
        {
            return null;
        }

        List<PdfOptionalContentStateChange> changes = new(stateArray.Count);
        var operation = PdfOptionalContentStateOperation.Unknown;

        for (int index = 0; index < stateArray.Count; index++)
        {
            PdfString? operationName = stateArray.GetName(index);
            if (operationName != null)
            {
                operation = operationName.Value.AsEnum<PdfOptionalContentStateOperation>();
                continue;
            }

            PdfReference? group = stateArray.GetReference(index);
            if (group != null && operation != PdfOptionalContentStateOperation.Unknown)
            {
                changes.Add(new PdfOptionalContentStateChange(operation, group.Value));
            }
        }

        return changes;
    }
}
