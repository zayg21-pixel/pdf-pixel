using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Measures;

/// <summary>
/// Base class for measure dictionaries: the scale and units of a measuring coordinate system.
/// </summary>
public abstract class PdfMeasure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfMeasure"/> class.
    /// </summary>
    /// <param name="subtype">The type of coordinate system.</param>
    private protected PdfMeasure(PdfMeasureType subtype) => Subtype = subtype;

    /// <summary>
    /// Gets the type of coordinate system the measure uses (/Subtype).
    /// </summary>
    public PdfMeasureType Subtype { get; }

    /// <summary>
    /// Returns the measure held by <paramref name="key"/> of <paramref name="owner"/>, using the document's cache if available.
    /// </summary>
    /// <param name="owner">Dictionary holding the measure entry.</param>
    /// <param name="key">Key of the measure entry.</param>
    /// <returns>The measure, or <see langword="null"/> when absent or not a dictionary.</returns>
    internal static PdfMeasure? GetMeasure(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    private static PdfMeasure? FromObject(PdfObject measureObject)
    {
        PdfDictionary? measureDictionary = measureObject.Value.AsDictionary();
        if (measureDictionary == null)
        {
            return null;
        }

        var subtype = PdfMeasureType.Rectilinear;
        PdfString? subtypeName = measureDictionary.GetName(PdfTokens.SubtypeKey);
        if (subtypeName != null)
        {
            subtype = subtypeName.Value.AsEnum<PdfMeasureType>();
        }

        return subtype switch
        {
            PdfMeasureType.Rectilinear => new PdfRectilinearMeasure(measureDictionary),
            PdfMeasureType.Geospatial => new PdfGeospatialMeasure(measureDictionary),
            _ => new PdfRawMeasure(measureDictionary, subtypeName)
        };
    }
}
