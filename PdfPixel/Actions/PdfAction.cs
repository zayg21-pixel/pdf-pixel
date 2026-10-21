using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Actions;

/// <summary>
/// Base class for PDF actions.
/// </summary>
/// <remarks>
/// Actions define behaviors to be performed in response to user interaction or other events.
/// </remarks>
public abstract class PdfAction
{
    private PdfDictionary? _actionDictionary;
    private bool _isNextRead;
    private List<PdfAction>? _next;

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
    /// Returns the actions performed after this one (/Next), in order, or <see langword="null"/> when absent.
    /// </summary>
    public IReadOnlyList<PdfAction>? GetNext()
    {
        if (!_isNextRead)
        {
            _isNextRead = true;
            _next = ReadNext();
        }

        return _next;
    }

    /// <summary>
    /// Returns the action held by <paramref name="key"/> of <paramref name="owner"/>, using the document's cache if available.
    /// </summary>
    /// <param name="owner">Dictionary holding the action entry.</param>
    /// <param name="key">Key of the action entry.</param>
    /// <returns>The action, or <see langword="null"/> when absent or not an action dictionary.</returns>
    internal static PdfAction? GetAction(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    /// <summary>
    /// Returns the action at <paramref name="index"/> of <paramref name="array"/>, using the document's cache if available.
    /// </summary>
    /// <param name="array">Array holding the action entry.</param>
    /// <param name="index">Index of the action entry.</param>
    /// <returns>The action, or <see langword="null"/> when absent or not an action dictionary.</returns>
    internal static PdfAction? GetAction(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    private static PdfAction? FromObject(PdfObject actionObject)
    {
        PdfDictionary? actionDictionary = actionObject.Value.AsDictionary();
        if (actionDictionary == null)
        {
            return null;
        }

        PdfActionType actionType = actionDictionary.GetNameOrDefault(PdfTokens.SKey).AsEnum<PdfActionType>();

        PdfAction action = actionType switch
        {
            PdfActionType.GoTo => new PdfGoToAction(actionDictionary),
            PdfActionType.GoToRemote => new PdfGoToRemoteAction(actionDictionary),
            PdfActionType.GoToEmbedded => new PdfGoToEmbeddedAction(actionDictionary),
            PdfActionType.Launch => new PdfLaunchAction(actionDictionary),
            PdfActionType.Thread => new PdfThreadAction(actionDictionary),
            PdfActionType.Hide => new PdfHideAction(actionDictionary),
            PdfActionType.Uri => new PdfUriAction(actionDictionary),
            PdfActionType.Named => new PdfNamedAction(actionDictionary),
            PdfActionType.SetOcgState => new PdfSetOcgStateAction(actionDictionary),
            _ => new PdfGenericAction(actionType, actionDictionary)
        };

        action._actionDictionary = actionDictionary;

        return action;
    }

    private List<PdfAction>? ReadNext()
    {
        if (_actionDictionary == null)
        {
            return null;
        }

        PdfAction? singleAction = GetAction(_actionDictionary, PdfTokens.NextKey);
        if (singleAction != null)
        {
            return new List<PdfAction> { singleAction };
        }

        PdfArray? nextArray = _actionDictionary.GetArray(PdfTokens.NextKey);
        if (nextArray == null)
        {
            return null;
        }

        List<PdfAction> nextActions = new(nextArray.Count);
        for (int index = 0; index < nextArray.Count; index++)
        {
            PdfAction? nextAction = GetAction(nextArray, index);
            if (nextAction != null)
            {
                nextActions.Add(nextAction);
            }
        }

        return nextActions;
    }
}
