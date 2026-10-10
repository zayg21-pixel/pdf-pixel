using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Actions.Model;

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
    /// <param name="actionType">The type of action.</param>
    private protected PdfAction(PdfActionType actionType) => ActionType = actionType;

    /// <summary>
    /// Gets the type of this action.
    /// </summary>
    public PdfActionType ActionType { get; }

    /// <summary>
    /// Actions performed after this one (/Next), in order, or <see langword="null"/> when absent.
    /// An action reached a second time through the chain is left out.
    /// </summary>
    public IReadOnlyList<PdfAction>? Next { get; private set; }

    /// <summary>
    /// Creates an action instance from a PDF dictionary.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing an action.</param>
    /// <returns>A concrete action instance, or null if the dictionary is null.</returns>
    internal static PdfAction? FromDictionary(PdfDictionary? actionDictionary)
    {
        if (actionDictionary == null)
        {
            return null;
        }

        return Create(actionDictionary, []);
    }

    private static PdfAction Create(PdfDictionary actionDictionary, HashSet<PdfReference> visitedActions)
    {
        PdfActionType actionType = actionDictionary.GetNameOrDefault(PdfTokens.SKey).AsEnum<PdfActionType>();

        PdfAction action = actionType switch
        {
            PdfActionType.GoTo => new PdfGoToAction(actionDictionary),
            PdfActionType.GoToRemote => new PdfGoToRemoteAction(actionDictionary),
            PdfActionType.Uri => new PdfUriAction(actionDictionary),
            PdfActionType.Named => new PdfNamedAction(actionDictionary),
            PdfActionType.SetOcgState => new PdfSetOcgStateAction(actionDictionary),
            _ => new PdfGenericAction(actionType)
        };

        action.Next = ReadNext(actionDictionary, visitedActions);

        return action;
    }

    private static List<PdfAction>? ReadNext(PdfDictionary actionDictionary, HashSet<PdfReference> visitedActions)
    {
        PdfArray? nextArray = actionDictionary.GetArray(PdfTokens.NextKey);
        if (nextArray != null)
        {
            List<PdfAction> nextActions = new(nextArray.Count);
            for (int index = 0; index < nextArray.Count; index++)
            {
                PdfAction? nextAction = ReadNextEntry(nextArray.GetDictionary(index), nextArray.GetReference(index), visitedActions);
                if (nextAction != null)
                {
                    nextActions.Add(nextAction);
                }
            }

            return nextActions;
        }

        PdfDictionary? nextDictionary = actionDictionary.GetDictionary(PdfTokens.NextKey);
        if (nextDictionary == null)
        {
            return null;
        }

        List<PdfAction> singleAction = [];
        PdfAction? singleNextAction = ReadNextEntry(nextDictionary, actionDictionary.GetReference(PdfTokens.NextKey), visitedActions);
        if (singleNextAction != null)
        {
            singleAction.Add(singleNextAction);
        }

        return singleAction;
    }

    private static PdfAction? ReadNextEntry(PdfDictionary? nextDictionary, PdfReference? nextReference, HashSet<PdfReference> visitedActions)
    {
        if (nextDictionary == null)
        {
            return null;
        }

        if (nextReference != null && !visitedActions.Add(nextReference.Value))
        {
            return null;
        }

        return Create(nextDictionary, visitedActions);
    }
}
