using PdfPixel.Functions;
using PdfPixel.Models;
using PdfPixel.Streams;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfPixel.Shading.Model;

/// <summary>
/// Parsed shading model extracted from a shading dictionary (PDF spec 8.7 Shadings).
/// Provides strongly-typed access to common keys for axial (type 2) and radial (type 3) shadings
/// and simple function based color interpolation.
/// </summary>
public sealed class PdfShading
{
    private PdfShading(PdfObject pdfObject)
    {
        PdfDictionary rawDictionary = pdfObject.Dictionary;
        SourceReference = pdfObject.Reference;

        if (rawDictionary == null)
        {
            ShadingType = PdfShadingType.Unknown;
            Functions = new List<PdfFunction>();
            return;
        }

        ShadingType = (PdfShadingType)rawDictionary.GetIntegerOrDefault(PdfTokens.ShadingTypeKey);
        Coords = rawDictionary.GetArray(PdfTokens.CoordsKey)?.GetFloatArray();
        float[]? domainArray = rawDictionary.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
        Domain = (domainArray != null) ? PdfRange.FromArray(domainArray) : null;

        PdfArray? extendArr = rawDictionary.GetArray(PdfTokens.ExtendKey);
        if (extendArr?.Count >= 2)
        {
            ExtendStart = extendArr.GetBooleanOrDefault(0);
            ExtendEnd = extendArr.GetBooleanOrDefault(1);
        }

        ColorSpaceObject = rawDictionary.GetObject(PdfTokens.ColorSpaceKey);
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
                float[]? decodeArray = rawDictionary.GetArray(PdfTokens.DecodeKey)?.GetFloatArray();
                Decode = (decodeArray != null) ? PdfRange.FromArray(decodeArray) : null;
                VerticesPerRow = rawDictionary.GetInteger(PdfTokens.VerticesPerRowKey);
                break;
            }
        }
    }

    /// <summary>
    /// Source object reference.
    /// </summary>
    public PdfReference SourceReference { get; }

    /// <summary>
    /// Returns a parsed PdfShading instance for the given shading object, using the document's cache if available.
    /// </summary>
    /// <param name="pdfObject">PDF shading object.</param>
    /// <returns>PdfShading instance, resolved from cache or newly parsed.</returns>
    public static PdfShading GetShading(PdfObject pdfObject)
    {
        if (pdfObject == null)
        {
            throw new ArgumentNullException(nameof(pdfObject));
        }

        if (pdfObject.Reference.IsValid)
        {
            Dictionary<PdfReference, PdfShading> cache = pdfObject.Document.ObjectCache.Shadings;
            if (cache.TryGetValue(pdfObject.Reference, out PdfShading? cachedShading))
            {
                return cachedShading;
            }
        }

        PdfShading shading = new(pdfObject);

        if (pdfObject.Reference.IsValid)
        {
            Dictionary<PdfReference, PdfShading> cache = pdfObject.Document.ObjectCache.Shadings;
            cache[pdfObject.Reference] = shading;
        }

        return shading;
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
    /// Domain (/Domain) defining parameter range for function evaluation (Type 1 - 3 specific).
    /// </summary>
    public PdfRange[]? Domain { get; }

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
    public PdfObject? ColorSpaceObject { get; }

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
    /// Decode (/Decode), mesh shading types 4-7.
    /// </summary>
    public PdfRange[]? Decode { get; }

    /// <summary>
    /// Vertices per row (/VerticesPerRow), lattice-form Gouraud type 5 only.
    /// </summary>
    public int? VerticesPerRow { get; }

    /// <summary>
    /// Shading matrix (/Matrix), function-based shading type 1.
    /// </summary>
    public SKMatrix? Matrix { get; }
}
