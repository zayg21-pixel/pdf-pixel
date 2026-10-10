using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Actions performed when a page is opened or closed (page /AA).
/// </summary>
public sealed class PdfPageAdditionalActions
{
    private PdfPageAdditionalActions(PdfAction? pageOpen, PdfAction? pageClose)
    {
        PageOpen = pageOpen;
        PageClose = pageClose;
    }

    /// <summary>
    /// Action performed when the page is opened (/O), after the document's open action,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfAction? PageOpen { get; }

    /// <summary>
    /// Action performed when the page is closed (/C), before any other page is opened,
    /// or <see langword="null"/> when absent.
    /// </summary>
    public PdfAction? PageClose { get; }

    /// <summary>
    /// Reads the page /AA entry, or <see langword="null"/> when it is absent.
    /// </summary>
    internal static PdfPageAdditionalActions? FromPage(PdfDictionary pageDictionary)
    {
        PdfDictionary? additionalActions = pageDictionary.GetDictionary(PdfTokens.AdditionalActionsKey);
        if (additionalActions == null)
        {
            return null;
        }

        PdfAction? pageOpen = PdfAction.FromDictionary(additionalActions.GetDictionary(PdfTokens.PageOpenKey));
        PdfAction? pageClose = PdfAction.FromDictionary(additionalActions.GetDictionary(PdfTokens.PageCloseKey));

        return new PdfPageAdditionalActions(pageOpen, pageClose);
    }
}
