using PdfPixel.Color.ColorSpace;
using PdfPixel.Functions;
using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Streams;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.Shading.Model;

/// <summary>
/// Parsed shading model extracted from a shading dictionary (PDF spec 8.7 Shadings).
/// Provides strongly-typed access to common keys for axial (type 2) and radial (type 3) shadings
/// and simple function based color interpolation.
/// </summary>
public sealed class PdfShading
{
    internal PdfShading(PdfObject pdfObject)
    {
        PdfDictionary rawDictionary = pdfObject.Dictionary;

        if (rawDictionary == null)
        {
            ShadingType = PdfShadingType.Unknown;
            Functions = new List<PdfFunction>();
            return;
        }

        ShadingType = (PdfShadingType)rawDictionary.GetIntegerOrDefault(PdfTokens.ShadingTypeKey);
        Coords = rawDictionary.GetArray(PdfTokens.CoordsKey)?.GetFloatArray();
        Domain = rawDictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();

        PdfArray? extendArr = rawDictionary.GetArray(PdfTokens.ExtendKey);
        if (extendArr?.Count >= 2)
        {
            ExtendStart = extendArr.GetBooleanOrDefault(0);
            ExtendEnd = extendArr.GetBooleanOrDefault(1);
        }

        ColorSpaceConverter = rawDictionary.GetObject(PdfTokens.ColorSpaceKey);
        Functions = new List<PdfFunction>();

        List<PdfObject>? functionObjects = rawDictionary.GetObjects(PdfTokens.FunctionKey);
        if (functionObjects != null)
        {
            foreach (PdfObject functionObject in functionObjects)
            {
                PdfFunction? function = PdfFunctions.GetFunction(functionObject);
                if (function != null)
                {
                    Functions.Add(function);
                }
            }
        }

        BBox = PdfLocationUtilities.CreateBBox(rawDictionary.GetArray(PdfTokens.BBoxKey));
        Background = rawDictionary.GetArray(PdfTokens.BackgroundKey)?.GetFloatArray();
        AntiAlias = rawDictionary.GetBooleanOrDefault(PdfTokens.AntiAliasKey);

        switch (ShadingType)
        {
            case PdfShadingType.FunctionBased:
            {
                Matrix = PdfLocationUtilities.CreateMatrix(rawDictionary.GetArray(PdfTokens.MatrixKey));
                break;
            }
            case PdfShadingType.FreeFormGouraud:
            case PdfShadingType.LatticeFormGouraud:
            case PdfShadingType.CoonsPatchMesh:
            case PdfShadingType.TensorProductPatchMesh:
            {
                Stream = pdfObject.Stream;
                BitsPerCoordinate = rawDictionary.GetInteger(PdfTokens.BitsPerCoordinateKey);
                BitsPerComponent = rawDictionary.GetInteger(PdfTokens.BitsPerComponentKey);
                BitsPerFlag = rawDictionary.GetInteger(PdfTokens.BitsPerFlagKey);
                DecodeArray = rawDictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();
                VerticesPerRow = rawDictionary.GetInteger(PdfTokens.VerticesPerRowKey);
                break;
            }
        }
    }

    /// <summary>
    /// Gets the self-contained stream source for mesh shading types (4-7). Null for non-mesh shadings.
    /// </summary>
    public PdfObjectStream? Stream { get; }

    /// <summary>
    /// Shading type (1 - 7 types).
    /// </summary>
    public PdfShadingType ShadingType { get; }

    /// <summary>
    /// Coordinates array (/Coords). Axial: [x0 y0 x1 y1]. Radial: [x0 y0 r0 x1 y1 r1], type 2, 3 specific.
    /// </summary>
    public float[]? Coords { get; }

    /// <summary>
    /// Domain array (/Domain) defining parameter range for function evaluation (Type 1 - 3 specific).
    /// </summary>
    public float[]? Domain { get; }

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
    public PdfObject? ColorSpaceConverter { get; }

    /// <summary>
    /// List of resolved PdfFunction(s) for function-based color evaluation.
    /// </summary>
    public List<PdfFunction> Functions { get; }

    /// <summary>
    /// Shading background color (/Background), if defined.
    /// </summary>
    public float[]? Background { get; }

    /// <summary>
    /// Gets the optional bbox (/BBox) for this shading, if defined.
    /// </summary>
    public SKRect? BBox { get; }

    /// <summary>
    /// Gets the option to enable anti-aliasing (/AntiAlias) when rendering the shading.
    /// </summary>
    public bool AntiAlias { get; }

    /// <summary>
    /// Bits per coordinate value (/BitsPerCoordinate), mesh shading types 4-7.
    /// </summary>
    public int? BitsPerCoordinate { get; }

    /// <summary>
    /// Bits per color component (/BitsPerComponent), mesh shading types 4-7.
    /// </summary>
    public int? BitsPerComponent { get; }

    /// <summary>
    /// Bits per flag (/BitsPerFlag), mesh shading types 4-7.
    /// </summary>
    public int? BitsPerFlag { get; }

    /// <summary>
    /// Decode array (/Decode), mesh shading types 4-7.
    /// </summary>
    public float[]? DecodeArray { get; }

    /// <summary>
    /// Vertices per row (/VerticesPerRow), lattice-form Gouraud type 5 only.
    /// </summary>
    public int? VerticesPerRow { get; }

    /// <summary>
    /// Shading matrix (/Matrix), function-based shading type 1.
    /// </summary>
    public SKMatrix? Matrix { get; }
}
