using PdfPixel.Text;

namespace PdfPixel.Models;

/// <summary>
/// Mark information dictionary (catalog /MarkInfo).
/// </summary>
public sealed class PdfMarkInformation
{
    internal PdfMarkInformation(PdfDictionary dictionary)
    {
        Marked = dictionary.GetBoolean(PdfTokens.MarkedKey);
        UserProperties = dictionary.GetBoolean(PdfTokens.MarkInfoUserPropertiesKey);
        Suspects = dictionary.GetBoolean(PdfTokens.SuspectsKey);
    }

    /// <summary>
    /// Whether the document conforms to tagged PDF conventions (/Marked), or <see langword="null"/> when absent.
    /// </summary>
    public bool? Marked { get; }

    /// <summary>
    /// Whether structure elements contain user properties attributes (/UserProperties),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public bool? UserProperties { get; }

    /// <summary>
    /// Whether tag suspects are present (/Suspects, deprecated in PDF 2.0), or <see langword="null"/> when absent.
    /// </summary>
    public bool? Suspects { get; }
}
