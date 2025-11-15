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
    public PdfShading(PdfObject pdfObject, PdfPage page)
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
            ExtendStart = extendArr.GetBool(0);
            ExtendEnd = extendArr.GetBool(1);
        }

        var colorSpaceValue = rawDictionary.GetValue(PdfTokens.ColorSpaceKey);
        ColorSpaceConverter = page.Cache.ColorSpace.ResolveByValue(colorSpaceValue);
        RenderingIntent = rawDictionary.GetName(PdfTokens.IntentKey).AsEnum<PdfRenderingIntent>();

        C0 = rawDictionary.GetArray(PdfTokens.C0Key)?.GetFloatArray();
        C1 = rawDictionary.GetArray(PdfTokens.C1Key)?.GetFloatArray();
        Functions = new List<PdfFunction>();

        var functionObjects = rawDictionary.GetPageObjects(PdfTokens.FunctionKey);
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

        var matrixArray = rawDictionary.GetArray(PdfTokens.MatrixKey);
        if (matrixArray != null && matrixArray.Count >= 6)
        {
            Matrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
        }
        else
        {
            Matrix = null;
        }
    }

    /// <summary>
    /// Gets the underlying <see cref="PdfObject"/> that serves as the source for this instance.
    /// </summary>
    public PdfObject SourceObject { get; }

    /// <summary>
    /// Shading type (2 = axial, 3 = radial, others may be added later).
    /// </summary>
    public int ShadingType { get; }

    /// <summary>
    /// Coordinates array (/Coords). Axial: [x0 y0 x1 y1]. Radial: [x0 y0 r0 x1 y1 r1].
    /// </summary>
    public float[] Coords { get; }

    /// <summary>
    /// Domain array (/Domain) defining parameter range for function evaluation.
    /// </summary>
    public float[] Domain { get; }

    /// <summary>
    /// Start extension flag (/Extend[0]).
    /// </summary>
    public bool ExtendStart { get; }

    /// <summary>
    /// End extension flag (/Extend[1]).
    /// </summary>
    public bool ExtendEnd { get; }

    /// <summary>
    /// Color space value (/ColorSpace) resolved object.
    /// </summary>
    public PdfColorSpaceConverter ColorSpaceConverter { get; }

    /// <summary>
    /// Gets the rendering intent used to control color rendering in the PDF.
    /// </summary>
    public PdfRenderingIntent RenderingIntent { get; }

    /// <summary>
    /// Optional start color array (/C0) for simple interpolation shadings.
    /// </summary>
    public float[] C0 { get; }

    /// <summary>
    /// Optional end color array (/C1) for simple interpolation shadings.
    /// </summary>
    public float[] C1 { get; }

    /// <summary>
    /// List of resolved PdfFunction(s) for function-based color evaluation.
    /// </summary>
    public List<PdfFunction> Functions { get; }

    /// <summary>
    /// Gets the optional transformation matrix (/Matrix) for this shading, if defined.
    /// </summary>
    public SKMatrix? Matrix { get; }
}
