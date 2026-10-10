using PdfPixel.Actions.Model;
using System;

namespace PdfPixel.PdfPanel.Actions;

/// <summary>
/// Arguments of a <see cref="PdfPanelActions"/> event raised for an action the host is asked to perform.
/// </summary>
/// <typeparam name="TAction">Type of the action.</typeparam>
public sealed class PdfPanelActionEventArgs<TAction> : EventArgs
    where TAction : PdfAction
{
    internal PdfPanelActionEventArgs(TAction action, PdfPanelActionSource source)
    {
        Action = action;
        Source = source;
    }

    /// <summary>
    /// The action performed.
    /// </summary>
    public TAction Action { get; }

    /// <summary>
    /// What triggered the action, or the action it is chained after.
    /// </summary>
    public PdfPanelActionSource Source { get; }
}
