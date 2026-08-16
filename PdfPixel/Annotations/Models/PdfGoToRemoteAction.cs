using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a GoToRemote action that changes the view to a specified destination in another document.
/// </summary>
public class PdfGoToRemoteAction : PdfAction
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfDestinationReference _destination;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToRemoteAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoToRemote action.</param>
    public PdfGoToRemoteAction(PdfDictionary actionDictionary)
        : base(actionDictionary, PdfActionType.GoToRemote)
    {
        _document = actionDictionary.Document;
        _destination = PdfDestinationReference.FromDictionary(actionDictionary, PdfTokens.DKey);
        FileSpecification = actionDictionary.GetString(PdfTokens.FKey);
        NewWindow = actionDictionary.GetBooleanOrDefault(PdfTokens.NewWindowKey);
    }

    /// <summary>
    /// Gets the file specification for the remote document.
    /// </summary>
    public PdfString? FileSpecification { get; }

    /// <summary>
    /// Gets a value indicating whether to open the destination in a new window.
    /// </summary>
    public bool NewWindow { get; }

    /// <summary>
    /// Gets the destination in the remote document. Its page belongs to that document, so it is
    /// resolved here only if this one has a page of the same number.
    /// </summary>
    public PdfDestination? GetDestination() => _document.Destinations.Resolve(_destination);

    /// <summary>
    /// Returns a string representation of this GoToRemote action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "GoToRemote Action";
}
