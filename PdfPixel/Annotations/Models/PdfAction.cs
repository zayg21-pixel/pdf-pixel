using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Base class for PDF actions.
/// </summary>
/// <remarks>
/// Actions define behaviors to be performed in response to user interaction or other events.
/// </remarks>
public abstract class PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the action.</param>
    /// <param name="actionType">The type of action.</param>
    protected PdfAction(PdfDictionary actionDictionary, PdfActionType actionType)
    {
        ActionDictionary = actionDictionary;
        ActionType = actionType;
        
        Next = actionDictionary?.GetValue(PdfTokens.NextKey);
    }

    /// <summary>
    /// Gets the PDF dictionary representing this action.
    /// </summary>
    public PdfDictionary ActionDictionary { get; }

    /// <summary>
    /// Gets the type of this action.
    /// </summary>
    public PdfActionType ActionType { get; }

    /// <summary>
    /// Gets the next action or array of actions to be performed after this one.
    /// </summary>
    /// <remarks>
    /// Can be a single action dictionary, an array of action dictionaries, or null.
    /// </remarks>
    public IPdfValue Next { get; }

    /// <summary>
    /// Creates an action instance from a PDF dictionary.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing an action.</param>
    /// <returns>A concrete action instance, or null if the dictionary is null or invalid.</returns>
    public static PdfAction FromDictionary(PdfDictionary actionDictionary)
    {
        if (actionDictionary == null)
        {
            return null;
        }

        var actionType = actionDictionary.GetName(PdfTokens.SKey).AsEnum<PdfActionType>();

        return actionType switch
        {
            PdfActionType.Uri => new PdfUriAction(actionDictionary),
            PdfActionType.GoTo => new PdfGoToAction(actionDictionary),
            PdfActionType.GoToRemote => new PdfGoToRemoteAction(actionDictionary),
            _ => new PdfGenericAction(actionDictionary, actionType)
        };
    }
}
