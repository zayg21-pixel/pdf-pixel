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
    public PdfPanelInterfaceActionEventArgs(PdfPanelInterfaceAction action) => Action = action;

    /// <summary>
    /// The requested action.
    /// </summary>
    public PdfPanelInterfaceAction Action { get; }
}
