using PdfReader.Rendering.Color;
using System.Collections.Generic;

namespace PdfReader.Models
{
    /// <summary>
    /// Parsed shading model extracted from a shading dictionary (PDF spec 8.7 Shadings).
    /// Provides strongly-typed access to common keys for axial (type 2) and radial (type 3) shadings
    /// and simple function based color interpolation.
    /// </summary>
    public sealed class PdfShading
    {
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

        public PdfShading(PdfDictionary raw, PdfPage page)
        {
            if (raw == null)
            {
                ShadingType = 0;
                Functions = new List<PdfFunction>();
                return;
            }
            ShadingType = raw.GetIntegerOrDefault(PdfTokens.ShadingTypeKey);
            var coordsArr = raw.GetArray(PdfTokens.CoordsKey)?.GetFloatArray();
            Coords = coordsArr;
            var domainArr = raw.GetArray(PdfTokens.DomainKey)?.GetFloatArray();
            Domain = domainArr;
            var extendArr = raw.GetArray(PdfTokens.ExtendKey);
            if (extendArr != null && extendArr.Count >= 2)
            {
                ExtendStart = extendArr.GetBool(0);
                ExtendEnd = extendArr.GetBool(1);
            }

            var colorSpaceValue = raw.GetValue(PdfTokens.ColorSpaceKey);
            ColorSpaceConverter = PdfColorSpaces.ResolveByValue(colorSpaceValue, page);

            C0 = raw.GetArray(PdfTokens.C0Key)?.GetFloatArray();
            C1 = raw.GetArray(PdfTokens.C1Key)?.GetFloatArray();
            Functions = new List<PdfFunction>();

            var functionObjects = raw.GetPageObjects(PdfTokens.FunctionKey);
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

            var functionValue = raw.GetValue(PdfTokens.FunctionKey);

            if (functionValue != null)
            {
                if (functionValue.Type == PdfValueType.Array)
                {
                    var functionArray = functionValue.AsArray();

                    for (int i = 0; i < functionArray.Count; i++)
                    {
                        var dictionaryValue = functionArray.GetValue(i);
                        if (dictionaryValue != null)
                        {
                            var funcObject = new PdfObject(new PdfReference(0, 0), page.Document, dictionaryValue);
                            var function = PdfFunctions.GetFunction(funcObject);

                            if (function != null)
                            {
                                Functions.Add(function);
                            }
                        }
                    }
                }
                else if (functionValue.Type == PdfValueType.Dictionary)
                {
                    var functionObject = new PdfObject(new PdfReference(0, 0), page.Document, functionValue);
                    var function = PdfFunctions.GetFunction(functionObject);
                    if (function != null)
                    {
                        Functions.Add(function);
                    }
                }
            }

        }
    }
}
