using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Measures;

/// <summary>
/// Viewport: a rectangular region of a page with its own measuring coordinate system.
/// </summary>
public sealed class PdfViewport
{
    private readonly PdfDictionary _dictionary;
    private List<PdfPointData>? _pointData;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfViewport"/> class.
    /// </summary>
    /// <param name="dictionary">The viewport dictionary.</param>
    internal PdfViewport(PdfDictionary dictionary)
    {
        _dictionary = dictionary;
        BoundingBox = PdfRectangle.FromArray(dictionary.GetArray(PdfTokens.BBoxKey));
        Name = dictionary.GetString(PdfTokens.ViewportNameKey);
        Measure = PdfMeasure.GetMeasure(dictionary, PdfTokens.MeasureKey);
    }

    /// <summary>
    /// Location of the viewport on the page in default user space (/BBox), or <see langword="null"/> when absent.
    /// </summary>
    public PdfRectangle? BoundingBox { get; }

    /// <summary>
    /// Title of the viewport (/Name), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? Name { get; }

    /// <summary>
    /// Scale and units for measurements within the viewport (/Measure), or <see langword="null"/> when absent.
    /// </summary>
    public PdfMeasure? Measure { get; }

    /// <summary>
    /// Returns the extended geospatial data of the viewport (/PtData). Empty when absent.
    /// </summary>
    public IReadOnlyList<PdfPointData> GetPointData()
    {
        if (_pointData == null)
        {
            _pointData = PdfPointData.FromOwner(_dictionary);
        }

        return _pointData;
    }

    /// <summary>
    /// Returns the viewports of <paramref name="page"/> (/VP) in drawing order.
    /// </summary>
    /// <param name="page">The page dictionary.</param>
    /// <returns>The viewports. Empty if the page has none.</returns>
    internal static List<PdfViewport> FromPage(PdfDictionary page)
    {
        List<PdfViewport> result = [];
        PdfArray? viewports = page.GetArray(PdfTokens.ViewportsKey);
        if (viewports == null)
        {
            return result;
        }

        for (int index = 0; index < viewports.Count; index++)
        {
            PdfDictionary? viewportDictionary = viewports.GetDictionary(index);
            if (viewportDictionary != null)
            {
                result.Add(new PdfViewport(viewportDictionary));
            }
        }

        return result;
    }
}
