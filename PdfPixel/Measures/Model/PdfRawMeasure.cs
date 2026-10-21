using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Measure dictionary of a coordinate system type no specification defines.
/// </summary>
public sealed class PdfRawMeasure : PdfMeasure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRawMeasure"/> class.
    /// </summary>
    /// <param name="measureDictionary">The measure dictionary.</param>
    /// <param name="rawSubtype">The subtype name as written.</param>
    internal PdfRawMeasure(PdfDictionary measureDictionary, PdfString? rawSubtype)
        : base(PdfMeasureType.Raw)
    {
        MeasureDictionary = measureDictionary;
        RawSubtype = rawSubtype;
    }

    /// <summary>
    /// The measure dictionary as written.
    /// </summary>
    public PdfDictionary MeasureDictionary { get; }

    /// <summary>
    /// The subtype name as written (/Subtype), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? RawSubtype { get; }
}
