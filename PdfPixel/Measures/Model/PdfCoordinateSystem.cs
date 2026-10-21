using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Geographic or projected coordinate system dictionary.
/// </summary>
public sealed class PdfCoordinateSystem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfCoordinateSystem"/> class.
    /// </summary>
    /// <param name="dictionary">The coordinate system dictionary.</param>
    internal PdfCoordinateSystem(PdfDictionary dictionary)
    {
        Type = dictionary.GetNameOrDefault(PdfTokens.TypeKey).AsEnum<PdfCoordinateSystemType>();
        Epsg = dictionary.GetInteger(PdfTokens.EpsgKey);
        WellKnownText = dictionary.GetString(PdfTokens.WellKnownTextKey);
    }

    /// <summary>
    /// Kind of coordinate system (/Type).
    /// </summary>
    public PdfCoordinateSystemType Type { get; }

    /// <summary>
    /// EPSG reference code (/EPSG), or <see langword="null"/> when absent.
    /// </summary>
    public int? Epsg { get; }

    /// <summary>
    /// Well Known Text description (/WKT), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? WellKnownText { get; }
}
