using PdfPixel.Models;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a GoToRemote action that changes the view to a specified destination in another document.
/// </summary>
public class PdfGoToRemoteAction : PdfFileAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToRemoteAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoToRemote action.</param>
    internal PdfGoToRemoteAction(PdfDictionary actionDictionary)
        : base(PdfActionType.GoToRemote, actionDictionary)
    {
        if (PdfDestinationResolver.ParseStructure(actionDictionary, PdfTokens.StructureDestinationKey) is PdfStructureIdDestination structureIdDestination)
        {
            Destination = structureIdDestination;
        }
        else
        {
            PdfDestination? destination = actionDictionary.Document.Destinations.Parse(actionDictionary, PdfTokens.DKey);
            if (destination is PdfNamedDestination || destination is PdfPageNumberDestination)
            {
                Destination = destination;
            }
        }
    }

    /// <summary>
    /// Destination in the remote document: the structure destination (/SD) when present, otherwise the
    /// destination (/D). It belongs to the remote document and is resolved by that document's
    /// <see cref="PdfDestinationResolver"/>.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Returns a string representation of this GoToRemote action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "GoToRemote Action";
}
