using PdfPixel.Models;
using PdfPixel.Navigation;
using PdfPixel.Navigation.Model;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// What happens when the document is opened (catalog /OpenAction): a destination to display or an action
/// to perform. Exactly one of <see cref="Destination"/> and <see cref="Action"/> is set.
/// </summary>
public sealed class PdfOpenAction
{
    private PdfOpenAction(PdfDestination? destination, PdfAction? action)
    {
        Destination = destination;
        Action = action;
    }

    /// <summary>
    /// Destination to display, resolved by this document's <see cref="PdfDestinationResolver"/>,
    /// or <see langword="null"/> when the document performs an action instead.
    /// </summary>
    public PdfDestination? Destination { get; }

    /// <summary>
    /// Action to perform, or <see langword="null"/> when the document displays a destination instead.
    /// </summary>
    public PdfAction? Action { get; }

    /// <summary>
    /// Reads the catalog /OpenAction entry, or <see langword="null"/> when it is absent or invalid.
    /// </summary>
    internal static PdfOpenAction? FromCatalog(PdfDictionary catalog, PdfDestinationResolver destinations)
    {
        PdfAction? action = PdfAction.FromDictionary(catalog.GetDictionary(PdfTokens.OpenActionKey));
        if (action != null)
        {
            return new PdfOpenAction(null, action);
        }

        PdfDestination? destination = destinations.Parse(catalog, PdfTokens.OpenActionKey);
        if (destination != null)
        {
            return new PdfOpenAction(destination, null);
        }

        return null;
    }
}
