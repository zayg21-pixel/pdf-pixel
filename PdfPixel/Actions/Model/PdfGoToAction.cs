using PdfPixel.Models;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a GoTo action that changes the view to a specified destination in the current document.
/// </summary>
public class PdfGoToAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoTo action.</param>
    internal PdfGoToAction(PdfDictionary actionDictionary)
        : base(PdfActionType.GoTo)
    {
        if (PdfDestinationResolver.ParseStructure(actionDictionary, PdfTokens.StructureDestinationKey) is PdfStructureDestination structureDestination)
        {
            Destination = structureDestination;
        }
        else
        {
            Destination = actionDictionary.Document.Destinations.Parse(actionDictionary, PdfTokens.DKey);
        }
    }

    /// <summary>
    /// Destination to display when this action is activated: the structure destination (/SD) when present,
    /// otherwise the destination (/D). Resolved by this document's <see cref="PdfDestinationResolver"/>.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Returns a string representation of this GoTo action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "GoTo Action";
}
