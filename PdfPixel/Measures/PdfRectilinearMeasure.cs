using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System;

namespace PdfPixel.Measures;

/// <summary>
/// Rectilinear measure dictionary: perpendicular axes with linearly incrementing units.
/// </summary>
public sealed class PdfRectilinearMeasure : PdfMeasure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfRectilinearMeasure"/> class.
    /// </summary>
    /// <param name="measureDictionary">The measure dictionary.</param>
    internal PdfRectilinearMeasure(PdfDictionary measureDictionary)
        : base(PdfMeasureType.Rectilinear)
    {
        ScaleRatio = measureDictionary.GetString(PdfTokens.ScaleRatioKey);
        XAxis = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.MeasureXKey)) ?? Array.Empty<PdfNumberFormat>();
        YAxis = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.MeasureYKey));
        Distance = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.DistanceFormatKey)) ?? Array.Empty<PdfNumberFormat>();
        Area = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.AreaFormatKey)) ?? Array.Empty<PdfNumberFormat>();
        Angle = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.AngleFormatKey));
        Slope = PdfNumberFormat.FromArray(measureDictionary.GetArray(PdfTokens.SlopeFormatKey));
        Origin = PdfPoint.FromArray(measureDictionary.GetArray(PdfTokens.MeasureOriginKey));
        YToXFactor = measureDictionary.GetFloat(PdfTokens.YToXFactorKey);
    }

    /// <summary>
    /// Text describing the scale ratio of the drawing (/R), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ScaleRatio { get; }

    /// <summary>
    /// Number formats for change along the x axis, and along the y axis when <see cref="YAxis"/> is absent (/X).
    /// Empty when absent.
    /// </summary>
    public PdfNumberFormat[] XAxis { get; }

    /// <summary>
    /// Number formats for change along the y axis (/Y), or <see langword="null"/> when absent.
    /// </summary>
    public PdfNumberFormat[]? YAxis { get; }

    /// <summary>
    /// Number formats for distance in any direction (/D). Empty when absent.
    /// </summary>
    public PdfNumberFormat[] Distance { get; }

    /// <summary>
    /// Number formats for area (/A). Empty when absent.
    /// </summary>
    public PdfNumberFormat[] Area { get; }

    /// <summary>
    /// Number formats for angles (/T), or <see langword="null"/> when absent.
    /// </summary>
    public PdfNumberFormat[]? Angle { get; }

    /// <summary>
    /// Number formats for the slope of a line (/S), or <see langword="null"/> when absent.
    /// </summary>
    public PdfNumberFormat[]? Slope { get; }

    /// <summary>
    /// Origin of the measuring coordinate system in default user space (/O), or <see langword="null"/> when absent.
    /// </summary>
    public PdfPoint? Origin { get; }

    /// <summary>
    /// Factor converting the largest y units to the largest x units (/CYX), or <see langword="null"/> when absent.
    /// </summary>
    public float? YToXFactor { get; }
}
