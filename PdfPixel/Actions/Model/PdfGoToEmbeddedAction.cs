using PdfPixel.Models;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a GoToEmbedded action that changes the view to a specified destination in an embedded document.
/// </summary>
public class PdfGoToEmbeddedAction : PdfFileAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToEmbeddedAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoToEmbedded action.</param>
    internal PdfGoToEmbeddedAction(PdfDictionary actionDictionary)
        : base(PdfActionType.GoToEmbedded, actionDictionary)
    {
        PdfDestination? destination = actionDictionary.Document.Destinations.Parse(actionDictionary, PdfTokens.DKey);
        if (destination is PdfNamedDestination || destination is PdfPageNumberDestination)
        {
            Destination = destination;
        }

        Path = ReadPath(actionDictionary);
    }

    /// <summary>
    /// Destination (/D) in the target document, resolved by that document's <see cref="PdfDestinationResolver"/>.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Steps from the start document to the target document (/T and the target dictionaries nested in it), in
    /// order; empty when the start document is the target. A step reached a second time is left out with the
    /// steps after it.
    /// </summary>
    public IReadOnlyList<PdfEmbeddedPathStep> Path { get; }

    private static List<PdfEmbeddedPathStep> ReadPath(PdfDictionary actionDictionary)
    {
        List<PdfEmbeddedPathStep> path = [];
        HashSet<PdfReference> visitedTargets = [];
        PdfDictionary currentDictionary = actionDictionary;

        while (true)
        {
            PdfReference? targetReference = currentDictionary.GetReference(PdfTokens.EmbeddedTargetKey);
            if (targetReference != null && !visitedTargets.Add(targetReference.Value))
            {
                return path;
            }

            PdfDictionary? targetDictionary = currentDictionary.GetDictionary(PdfTokens.EmbeddedTargetKey);
            if (targetDictionary == null)
            {
                return path;
            }

            path.Add(new PdfEmbeddedPathStep(targetDictionary));
            currentDictionary = targetDictionary;
        }
    }

    /// <summary>
    /// Returns a string representation of this GoToEmbedded action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "GoToEmbedded Action";
}
