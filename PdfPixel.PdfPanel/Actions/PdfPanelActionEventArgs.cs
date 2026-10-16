using PdfPixel.Actions.Model;
using System;

namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// Arguments of <see cref="PdfPanelActions.ActionRequested"/>, raised for an action the host is asked to perform.
/// </summary>
public sealed class PdfPanelActionEventArgs : EventArgs
{
    internal PdfPanelActionEventArgs(PdfAction action, PdfPanelActionSource source, object sourceObject, object documentLocker)
    {
        Action = action;
        Source = source;
        SourceObject = sourceObject;
        DocumentLocker = documentLocker;
    }

    /// <summary>
    /// The action performed.
    /// </summary>
    public PdfAction Action { get; }

    /// <summary>
    /// What triggered the action.
    /// </summary>
    public PdfPanelActionSource Source { get; }

    /// <summary>
    /// Object that triggered the action.
    /// </summary>
    public object SourceObject { get; }

    /// <summary>
    /// Object to lock while reading the document from the handler.
    /// </summary>
    public object DocumentLocker { get; }
}
