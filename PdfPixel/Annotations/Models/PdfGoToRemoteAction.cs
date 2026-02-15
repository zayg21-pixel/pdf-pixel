using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a GoToRemote action that changes the view to a specified destination in another document.
/// </summary>
public class PdfGoToRemoteAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGoToRemoteAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the GoToRemote action.</param>
    public PdfGoToRemoteAction(PdfDictionary actionDictionary)
        : base(actionDictionary, PdfActionType.GoToRemote)
    {
        FileSpecification = actionDictionary.GetString(PdfTokens.FKey);
        Destination = PdfDestination.Parse(actionDictionary.GetValue(PdfTokens.DKey), actionDictionary.Document);
        NewWindow = actionDictionary.GetBooleanOrDefault(PdfTokens.NewWindowKey);
    }

    /// <summary>
    /// Gets the file specification for the remote document.
    /// </summary>
    public PdfString FileSpecification { get; }

    /// <summary>
    /// Gets the parsed destination in the remote document.
    /// </summary>
    public PdfDestination Destination { get; }

    /// <summary>
    /// Gets a value indicating whether to open the destination in a new window.
    /// </summary>
    public bool NewWindow { get; }

    /// <summary>
    /// Returns a string representation of this GoToRemote action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString()
    {
        return "GoToRemote Action";
    }
}
