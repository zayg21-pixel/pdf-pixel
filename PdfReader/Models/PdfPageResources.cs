using PdfReader.Text;
using SkiaSharp;

namespace PdfReader.Models
{
    /// <summary>
    /// Accumulates inheritable page-tree attributes (Resources, MediaBox, CropBox, Rotate and other box types)
    /// for top-down traversal. Each child overrides previously inherited values for any keys it defines.
    /// Only stores parsed <see cref="SKRect"/> values for box arrays (no raw PdfArray retention).
    /// Rotation value is normalized here (0,90,180,270) when encountered.
    /// </summary>
    public sealed class PdfPageResources
    {
        /// <summary>
        /// Current effective resource dictionary (/Resources).
        /// </summary>
        public PdfDictionary Resources { get; private set; }

        /// <summary>
        /// Current effective /MediaBox rectangle (may be null if not yet defined or malformed array shorter than4 entries).
        /// </summary>
        public SKRect? MediaBoxRect { get; private set; }

        /// <summary>
        /// Current effective /CropBox rectangle (may be null if not yet defined or malformed array shorter than4 entries).
        /// </summary>
        public SKRect? CropBoxRect { get; private set; }

        /// <summary>
        /// Current effective /Rotate value (degrees).
        /// </summary>
        public int? Rotate { get; private set; }

        /// <summary>
        /// Current effective /BleedBox rectangle.
        /// </summary>
        public SKRect? BleedBoxRect { get; private set; }

        /// <summary>
        /// Current effective /TrimBox rectangle.
        /// </summary>
        public SKRect? TrimBoxRect { get; private set; }

        /// <summary>
        /// Current effective /ArtBox rectangle.
        /// </summary>
        public SKRect? ArtBoxRect { get; private set; }

        /// <summary>
        /// Create a shallow clone of the current effective values. Resource dictionary reference is reused.
        /// </summary>
        /// <returns>New <see cref="PdfPageResources"/> instance with copied values.</returns>
        public PdfPageResources Clone()
        {
            var copy = new PdfPageResources();
            copy.Resources = Resources; // reuse reference (immutable usage expected)
            copy.MediaBoxRect = MediaBoxRect;
            copy.CropBoxRect = CropBoxRect;
            copy.Rotate = Rotate;
            copy.BleedBoxRect = BleedBoxRect;
            copy.TrimBoxRect = TrimBoxRect;
            copy.ArtBoxRect = ArtBoxRect;
            return copy;
        }

        /// <summary>
        /// Update effective values from a /Pages or /Page object. Any key present replaces the prior value; missing keys leave existing values unchanged.
        /// No NaN or dimension validation is performed; rectangles are taken as-is when4 numeric entries are present.
        /// </summary>
        /// <param name="pdfObject">Source /Pages or /Page object.</param>
        public void UpdateFrom(PdfObject pdfObject)
        {
            if (pdfObject == null)
            {
                return;
            }
            var dict = pdfObject.Dictionary;
            if (dict == null)
            {
                return;
            }

            if (dict.HasKey(PdfTokens.ResourcesKey))
            {
                Resources = dict.GetDictionary(PdfTokens.ResourcesKey);
            }
            if (dict.HasKey(PdfTokens.MediaBoxKey))
            {
                MediaBoxRect = ParseRect(dict.GetValue(PdfTokens.MediaBoxKey).AsArray());
            }
            if (dict.HasKey(PdfTokens.CropBoxKey))
            {
                CropBoxRect = ParseRect(dict.GetValue(PdfTokens.CropBoxKey).AsArray());
            }
            if (dict.HasKey(PdfTokens.RotateKey))
            {
                Rotate = NormalizeRotation(dict.GetIntegerOrDefault(PdfTokens.RotateKey));
            }
            if (dict.HasKey(PdfTokens.BleedBoxKey))
            {
                BleedBoxRect = ParseRect(dict.GetValue(PdfTokens.BleedBoxKey).AsArray());
            }
            if (dict.HasKey(PdfTokens.TrimBoxKey))
            {
                TrimBoxRect = ParseRect(dict.GetValue(PdfTokens.TrimBoxKey).AsArray());
            }
            if (dict.HasKey(PdfTokens.ArtBoxKey))
            {
                ArtBoxRect = ParseRect(dict.GetValue(PdfTokens.ArtBoxKey).AsArray());
            }
        }

        /// <summary>
        /// Convert a PDF box array [llx lly urx ury] (lower-left, lower-left, upper-right, upper-right) to an <see cref="SKRect"/>.
        /// Returns null only if array is null or has fewer than4 items.
        /// </summary>
        /// <param name="array">Source PDF array.</param>
        private SKRect? ParseRect(PdfArray array)
        {
            if (array == null)
            {
                return null;
            }
            if (array.Count <4)
            {
                return null;
            }
            float lowerLeftX = array.GetFloat(0);
            float lowerLeftY = array.GetFloat(1);
            float upperRightX = array.GetFloat(2);
            float upperRightY = array.GetFloat(3);
            return new SKRect(lowerLeftX, lowerLeftY, upperRightX, upperRightY);
        }

        private static int NormalizeRotation(int rotation)
        {
            return (rotation %360 +360) %360;
        }
    }
}
