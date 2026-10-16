using PdfPixel.Navigation.Model;
using System;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Arguments of <see cref="PdfPanelInterface.Requested"/>.
/// </summary>
public sealed class PdfPanelInterfaceActionEventArgs : EventArgs
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPanelInterfaceActionEventArgs"/> class.
    /// </summary>
    /// <param name="action">The requested action.</param>
    /// <param name="destination">Destination of <see cref="PdfPanelInterfaceAction.ScrollToDestination"/>, otherwise <see langword="null"/>.</param>
    public PdfPanelInterfaceActionEventArgs(PdfPanelInterfaceAction action, PdfDestination? destination)
    {
        Action = action;
        Destination = destination;
    }

    /// <summary>
    /// The requested action.
    /// </summary>
    public PdfPanelInterfaceAction Action { get; }

    /// <summary>
    /// Destination of <see cref="PdfPanelInterfaceAction.ScrollToDestination"/>, otherwise <see langword="null"/>.
    /// </summary>
    public PdfDestination? Destination { get; }
}
