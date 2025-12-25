using PdfReader.Color.ColorSpace;
using PdfReader.Functions;
using PdfReader.Models;
using PdfReader.Rendering.Operators;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Shading.Model;

/// <summary>
/// Parsed shading model extracted from a shading dictionary (PDF spec 8.7 Shadings).
/// Provides strongly-typed access to common keys for axial (type 2) and radial (type 3) shadings
/// and simple function based color interpolation.
/// </summary>
public sealed class PdfShading
{
    public PdfShading(PdfObject pdfObject)
    {
        var rawDictionary = pdfObject.Dictionary;
        SourceObject = pdfObject;

        if (rawDictionary == null)
        {
            ShadingType = 0;
            Functions = new List<PdfFunction>();
            return;
        }

        ShadingType = rawDictionary.GetIntegerOrDefault(PdfTokens.ShadingTypeKey);
        var coordsArr = rawDictionary.GetArray(PdfTokens.CoordsKey)?.GetFloatArray();
        Coords = coordsArr;
        var domainArr = rawDictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        Domain = domainArr;
        var extendArr = rawDictionary.GetArray(PdfTokens.ExtendKey);
        if (extendArr != null && extendArr.Count >= 2)
        {
            ExtendStart = extendArr.GetBoolean(0);
            ExtendEnd = extendArr.GetBoolean(1);
        }

        ColorSpaceConverter = rawDictionary.GetObject(PdfTokens.ColorSpaceKey);
        Functions = new List<PdfFunction>();

        var functionObjects = rawDictionary.GetObjects(PdfTokens.FunctionKey);
        if (functionObjects != null)
        {
            foreach (var functionObject in functionObjects)
            {
                var function = PdfFunctions.GetFunction(functionObject);
                if (function != null)
                {
                    Functions.Add(function);
                }
            }
        }

        var bboxArray = rawDictionary.GetArray(PdfTokens.BBoxKey);
        BBox = PdfLocationUtilities.CreateBBox(bboxArray);
        Background = rawDictionary.GetArray(PdfTokens.BackgroundKey)?.GetFloatArray();
        AntiAlias = rawDictionary.GetBooleanOrDefault(PdfTokens.AntiAliasKey);
    }

    /// <summary>
    /// Gets the underlying <see cref="PdfObject"/> that serves as the source for this instance.
    /// </summary>
    public PdfObject SourceObject { get; }

    /// <summary>
    /// Shading type (1 - 7 types).
    /// </summary>
    public int ShadingType { get; }

    /// <summary>
    /// Coordinates array (/Coords). Axial: [x0 y0 x1 y1]. Radial: [x0 y0 r0 x1 y1 r1], type 2, 3 specific.
    /// </summary>
    public float[] Coords { get; }

    /// <summary>
    /// Domain array (/Domain) defining parameter range for function evaluation (Type 1 - 3 specific).
    /// </summary>
    public float[] Domain { get; }

    /// <summary>
    /// Start extension flag (/Extend[0]), type 2, 3 specific.
    /// </summary>
    public bool ExtendStart { get; }

    /// <summary>
    /// End extension flag (/Extend[1]), type 2, 3 specific.
    /// </summary>
    public bool ExtendEnd { get; }

    /// <summary>
    /// Color space value (/ColorSpace) object.
    /// </summary>
    public PdfObject ColorSpaceConverter { get; }

    /// <summary>
    /// List of resolved PdfFunction(s) for function-based color evaluation.
    /// </summary>
    public List<PdfFunction> Functions { get; }

    /// <summary>
    /// Shading background color (/Background), if defined.
    /// </summary>
    public float[] Background { get; }

    /// <summary>
    /// Gets the optional bbox (/BBox) for this shading, if defined.
    /// </summary>
    public SKRect? BBox { get; }

    /// <summary>
    /// Gets the option to enable anti-aliasing (/AntiAlias) when rendering the shading.
    /// </summary>
    public bool AntiAlias { get; }
}