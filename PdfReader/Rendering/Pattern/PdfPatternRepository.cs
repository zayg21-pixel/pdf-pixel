using System.Collections.Generic;
using PdfReader.Models;

namespace PdfReader.Rendering.Pattern
{
    // TODO: currenlty not used - implement pattern painting and caching
    /// <summary>
    /// Repository/cache for parsed pattern objects. Currently stores both tiling and shading patterns.
    /// </summary>
    internal sealed class PdfPatternRepository
    {
        private readonly Dictionary<PdfReference, PdfPattern> _patterns = new Dictionary<PdfReference, PdfPattern>();

        public bool TryGet(PdfReference reference, out PdfPattern pattern)
        {
            return _patterns.TryGetValue(reference, out pattern);
        }

        public bool TryGetTiling(PdfReference reference, out PdfTilingPattern tilingPattern)
        {
            if (_patterns.TryGetValue(reference, out var p) && p is PdfTilingPattern t)
            {
                tilingPattern = t;
                return true;
            }
            tilingPattern = null;
            return false;
        }

        public bool TryGetShading(PdfReference reference, out PdfShadingPattern shadingPattern)
        {
            if (_patterns.TryGetValue(reference, out var p) && p is PdfShadingPattern s)
            {
                shadingPattern = s;
                return true;
            }
            shadingPattern = null;
            return false;
        }

        public void Store(PdfPattern pattern)
        {
            if (pattern != null && pattern.Reference.IsValid)
            {
                _patterns[pattern.Reference] = pattern;
            }
        }
    }
}
