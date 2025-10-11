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
        /// Underlying raw dictionary for fallback access (still needed for function evaluation).
        /// </summary>
        public PdfDictionary RawDictionary { get; } // TODO: need to replace with function object model, like function evaluator

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
        /// Color space value (/ColorSpace) raw object for converter resolution.
        /// </summary>
        public IPdfValue ColorSpaceValue { get; }

        /// <summary>
        /// Optional start color array (/C0) for simple interpolation shadings.
        /// </summary>
        public float[] C0 { get; }

        /// <summary>
        /// Optional end color array (/C1) for simple interpolation shadings.
        /// </summary>
        public float[] C1 { get; }

        /// <summary>
        /// True when a /Function entry is present (function-based color evaluation instead of simple C0/C1).
        /// </summary>
        public bool HasFunction { get; }

        public PdfShading(PdfDictionary raw)
        {
            RawDictionary = raw;
            if (raw == null)
            {
                ShadingType = 0;
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
            ColorSpaceValue = raw.GetValue(PdfTokens.ColorSpaceKey);
            C0 = raw.GetArray(PdfTokens.C0Key)?.GetFloatArray();
            C1 = raw.GetArray(PdfTokens.C1Key)?.GetFloatArray();
            HasFunction = raw.GetValue(PdfTokens.FunctionKey) != null;
        }
    }
}
