using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Text;
using System;

namespace PdfPixel.Measures.Model;

/// <summary>
/// Geospatial measure dictionary: relates object space to a coordinate system on a model of the earth.
/// </summary>
public sealed class PdfGeospatialMeasure : PdfMeasure
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfGeospatialMeasure"/> class.
    /// </summary>
    /// <param name="measureDictionary">The measure dictionary.</param>
    internal PdfGeospatialMeasure(PdfDictionary measureDictionary)
        : base(PdfMeasureType.Geospatial)
    {
        PdfArray? boundsArray = measureDictionary.GetArray(PdfTokens.BoundsKey);
        if (boundsArray != null)
        {
            Bounds = PdfPoint.ParsePoints(boundsArray);
        }
        else
        {
            Bounds = [new PdfPoint(0f, 0f), new PdfPoint(0f, 1f), new PdfPoint(1f, 1f), new PdfPoint(1f, 0f)];
        }

        PdfDictionary? coordinateSystemDictionary = measureDictionary.GetDictionary(PdfTokens.CoordinateSystemKey);
        if (coordinateSystemDictionary != null)
        {
            CoordinateSystem = new PdfCoordinateSystem(coordinateSystemDictionary);
        }

        PdfDictionary? displayCoordinateSystemDictionary = measureDictionary.GetDictionary(PdfTokens.DisplayCoordinateSystemKey);
        if (displayCoordinateSystemDictionary != null)
        {
            DisplayCoordinateSystem = new PdfCoordinateSystem(displayCoordinateSystemDictionary);
        }

        PdfArray? displayUnits = measureDictionary.GetArray(PdfTokens.PreferredDisplayUnitsKey);
        LinearDisplayUnit = displayUnits?.GetName(0)?.AsEnum<PdfLinearDisplayUnit>();
        AreaDisplayUnit = displayUnits?.GetName(1)?.AsEnum<PdfAreaDisplayUnit>();
        AngularDisplayUnit = displayUnits?.GetName(2)?.AsEnum<PdfAngularDisplayUnit>();

        GeographicPoints = measureDictionary.GetArray(PdfTokens.GeographicPointsKey)?.GetFloatArray() ?? Array.Empty<float>();
        LocalPoints = measureDictionary.GetArray(PdfTokens.LocalPointsKey)?.GetFloatArray();
        ProjectedCoordinateSystemMatrix = measureDictionary.GetArray(PdfTokens.ProjectedCoordinateSystemMatrixKey)?.GetFloatArray();
    }

    /// <summary>
    /// Polygon, relative to the unit square of the owner's bounds, in which the geospatial transformation is valid (/Bounds).
    /// The full unit square when absent.
    /// </summary>
    public PdfPoint[] Bounds { get; }

    /// <summary>
    /// Geographic or projected coordinate system of the points (/GCS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfCoordinateSystem? CoordinateSystem { get; }

    /// <summary>
    /// Coordinate system for displaying position values (/DCS), or <see langword="null"/> when absent.
    /// </summary>
    public PdfCoordinateSystem? DisplayCoordinateSystem { get; }

    /// <summary>
    /// Preferred unit for displaying positions and distances (first /PDU entry), or <see langword="null"/> when absent.
    /// </summary>
    public PdfLinearDisplayUnit? LinearDisplayUnit { get; }

    /// <summary>
    /// Preferred unit for displaying areas (second /PDU entry), or <see langword="null"/> when absent.
    /// </summary>
    public PdfAreaDisplayUnit? AreaDisplayUnit { get; }

    /// <summary>
    /// Preferred unit for displaying angles (third /PDU entry), or <see langword="null"/> when absent.
    /// </summary>
    public PdfAngularDisplayUnit? AngularDisplayUnit { get; }

    /// <summary>
    /// Point coordinates in the coordinate system (/GPTS): latitude and longitude pairs, easting and northing pairs,
    /// or triples with elevation for 3D. Empty when absent.
    /// </summary>
    public float[] GeographicPoints { get; }

    /// <summary>
    /// Unit square positions matching <see cref="GeographicPoints"/> (/LPTS), or <see langword="null"/> when absent.
    /// </summary>
    public float[]? LocalPoints { get; }

    /// <summary>
    /// 12-element matrix from object coordinates to the projected coordinate system (/PCSM),
    /// or <see langword="null"/> when absent.
    /// </summary>
    public float[]? ProjectedCoordinateSystemMatrix { get; }
}
