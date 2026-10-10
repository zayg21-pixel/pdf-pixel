using PdfPixel.Navigation;

namespace PdfPixel.Navigation.Model;

/// <summary>
/// A destination as written: a particular view of the document it belongs to, resolved into a
/// <see cref="PdfNavigationTarget"/> by that document's <see cref="PdfDestinationResolver"/>.
/// </summary>
public abstract class PdfDestination
{
    private protected PdfDestination()
    {
    }
}
