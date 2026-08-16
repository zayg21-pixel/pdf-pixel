using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a GoTo action that changes the view to a specified destination in the current document.
/// </summary>
public class PdfGoToAction : PdfAction
{
    private readonly IPdfDocumentInternal _document;
    private readonly PdfDestinationReference _destination;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoTo action.</param>
    public PdfGoToAction(PdfDictionary actionDictionary)
        : base(actionDictionary, PdfActionType.GoTo)
    {
        _document = actionDictionary.Document;
        _destination = PdfDestinationReference.FromDictionary(actionDictionary, PdfTokens.DKey);
    }

    /// <summary>
    /// Gets the destination to display when this action is activated.
    /// </summary>
    public PdfDestination? GetDestination() => _document.Destinations.Resolve(_destination);

    /// <summary>
    /// Returns a string representation of this GoTo action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "GoTo Action";
}
