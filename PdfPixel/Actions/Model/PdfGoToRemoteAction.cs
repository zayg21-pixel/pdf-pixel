using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a GoToRemote action that changes the view to a specified destination in another document.
/// </summary>
public class PdfGoToRemoteAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToRemoteAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoToRemote action.</param>
    internal PdfGoToRemoteAction(PdfDictionary actionDictionary)
        : base(PdfActionType.GoToRemote)
    {
        FileSpecification = PdfFileSpecification.FromDictionaryEntry(actionDictionary, PdfTokens.FKey);
        NewWindow = actionDictionary.GetBoolean(PdfTokens.NewWindowKey);

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
    /// Gets the file specification for the remote document.
    /// </summary>
    public PdfFileSpecification? FileSpecification { get; }

    /// <summary>
    /// Whether to open the remote document in a new window (/NewWindow), or <see langword="null"/> when the
    /// viewer decides.
    /// </summary>
    public bool? NewWindow { get; }

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
