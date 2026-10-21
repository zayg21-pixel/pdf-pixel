using PdfPixel.Objects;
using PdfPixel.Parsing;
using System.Collections.Generic;

namespace PdfPixel.Measures;

/// <summary>
/// Point data dictionary: extended geospatial data as tuples of named values.
/// </summary>
public sealed class PdfPointData
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfPointData"/> class.
    /// </summary>
    /// <param name="dictionary">The point data dictionary.</param>
    internal PdfPointData(PdfDictionary dictionary)
    {
        Subtype = dictionary.GetNameOrDefault(PdfTokens.SubtypeKey).AsEnum<PdfPointDataType>();

        List<PdfString> names = [];
        PdfArray? namesArray = dictionary.GetArray(PdfTokens.PointDataNamesKey);
        if (namesArray != null)
        {
            for (int index = 0; index < namesArray.Count; index++)
            {
                names.Add(namesArray.GetNameOrDefault(index));
            }
        }

        Names = names.ToArray();

        List<PdfValue[]> points = [];
        PdfArray? pointsArray = dictionary.GetArray(PdfTokens.PointsKey);
        if (pointsArray != null)
        {
            for (int pointIndex = 0; pointIndex < pointsArray.Count; pointIndex++)
            {
                PdfArray? tupleArray = pointsArray.GetArray(pointIndex);
                if (tupleArray != null)
                {
                    points.Add(ParseTuple(tupleArray));
                }
            }
        }

        Points = points.ToArray();
    }

    /// <summary>
    /// Type of the point data (/Subtype).
    /// </summary>
    public PdfPointDataType Subtype { get; }

    /// <summary>
    /// Names of the tuple members, in tuple order (/Names): LAT, LON and ALT, or other names.
    /// Empty when absent.
    /// </summary>
    public PdfString[] Names { get; }

    /// <summary>
    /// Point tuples, each typed by the matching entry of <see cref="Names"/> (/XPTS). Empty when absent.
    /// </summary>
    public PdfValue[][] Points { get; }

    /// <summary>
    /// Returns the point data of <paramref name="owner"/> (/PtData), a single dictionary or an array of them.
    /// </summary>
    /// <param name="owner">Dictionary holding the point data entry.</param>
    /// <returns>The point data in order. Empty if absent.</returns>
    internal static List<PdfPointData> FromOwner(PdfDictionary owner)
    {
        List<PdfPointData> result = [];
        PdfArray? pointDataArray = owner.GetArray(PdfTokens.PointDataKey);
        if (pointDataArray != null)
        {
            for (int index = 0; index < pointDataArray.Count; index++)
            {
                PdfPointData? pointData = GetPointData(pointDataArray, index);
                if (pointData != null)
                {
                    result.Add(pointData);
                }
            }
        }
        else
        {
            PdfPointData? pointData = GetPointData(owner, PdfTokens.PointDataKey);
            if (pointData != null)
            {
                result.Add(pointData);
            }
        }

        return result;
    }

    private static PdfPointData? GetPointData(PdfDictionary owner, in PdfString key)
        => owner.Document.ObjectCache.GetOrCreate(owner, key, FromObject);

    private static PdfPointData? GetPointData(PdfArray array, int index)
        => array.Document.ObjectCache.GetOrCreate(array, index, FromObject);

    private static PdfPointData? FromObject(PdfObject pointDataObject)
    {
        PdfDictionary? dictionary = pointDataObject.Value.AsDictionary();
        if (dictionary == null)
        {
            return null;
        }

        return new PdfPointData(dictionary);
    }

    private static PdfValue[] ParseTuple(PdfArray tupleArray)
    {
        List<PdfValue> values = new(tupleArray.Count);
        for (int index = 0; index < tupleArray.Count; index++)
        {
            PdfValue? value = tupleArray.GetValue(index);
            if (value != null)
            {
                values.Add(value);
            }
        }

        return values.ToArray();
    }
}
