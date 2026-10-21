using PdfPixel.Actions;
using PdfPixel.PdfPanel.Actions;
using System;
using System.Windows;

namespace PdfPixel.PdfPanel.Wpf;

/// <summary>
/// Arguments of <see cref="WpfPdfPanel.ActionRequested"/>, raised for an action the host is asked to perform.
/// </summary>
public sealed class WpfPdfPanelActionEventArgs : RoutedEventArgs
{
    internal WpfPdfPanelActionEventArgs(RoutedEvent routedEvent, PdfPanelActionEventArgs args)
        : base(routedEvent)
    {
        Action = args.Action;
        ActionSource = args.Source;
        SourceObject = args.SourceObject;
        DocumentLocker = args.DocumentLocker;
    }

    /// <summary>
    /// The action performed.
    /// </summary>
    public PdfAction Action { get; }

    /// <summary>
    /// What triggered the action.
    /// </summary>
    public PdfPanelActionSource ActionSource { get; }

    /// <summary>
    /// Object that triggered the action.
    /// </summary>
    public object SourceObject { get; }

    /// <summary>
    /// Object to lock while reading the document from the handler.
    /// </summary>
    public object DocumentLocker { get; }

    /// <inheritdoc />
    protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
    {
        if (genericHandler == null)
        {
            throw new ArgumentNullException(nameof(genericHandler));
        }

        ((EventHandler<WpfPdfPanelActionEventArgs>)genericHandler)(genericTarget, this);
    }
}
