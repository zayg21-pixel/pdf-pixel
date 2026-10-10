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
    internal PdfPanelActionEventArgs(TAction action) => Action = action;

    /// <summary>
    /// The action performed.
    /// </summary>
    public TAction Action { get; }
}
