using PdfPixel.Models;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a generic PDF action when the specific action type is not implemented.
/// </summary>
public class PdfGenericAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGenericAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the action.</param>
    /// <param name="actionType">The type of action.</param>
    public PdfGenericAction(PdfDictionary actionDictionary, PdfActionType actionType)
        : base(actionDictionary, actionType)
    {
    }

    /// <summary>
    /// Returns a string representation of this generic action.
    /// </summary>
    /// <returns>A string containing the action type.</returns>
    public override string ToString()
    {
        return $"Action: {ActionType}";
    }
}
