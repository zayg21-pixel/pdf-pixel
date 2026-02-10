using PdfPixel.Models;
using PdfPixel.Rendering.Operators;
using PdfPixel.Text;
using PdfPixel.Transparency.Model;
using PdfPixel.Transparency.Utilities;
using SkiaSharp;
using System;

namespace PdfPixel.Forms
{
    /// <summary>
    /// Represents a parsed PDF Form XObject with geometry, resources, transparency group, parent page, and original object.
    /// </summary>
    public class PdfForm
    {
        private PdfForm(
            SKMatrix matrix,
            SKRect bbox,
            PdfTransparencyGroup transparencyGroup,
            PdfDictionary dictionary,
            PdfDictionary resources,
            PdfPage page,
            PdfObject xObject)
        {
            Matrix = matrix;
            BBox = bbox;
            TransparencyGroup = transparencyGroup;
            Dictionary = dictionary;
            Resources = resources;
            Page = page;
            XObject = xObject;
        }

        /// <summary>
        /// The transformation matrix (/Matrix) for the form. Identity if not specified.
        /// </summary>
        public SKMatrix Matrix { get; }

        /// <summary>
        /// The bounding box (/BBox) for the form. Empty if not specified.
        /// </summary>
        public SKRect BBox { get; }

        /// <summary>
        /// The transparency group (/Group) for the form, if present.
        /// </summary>
        public PdfTransparencyGroup TransparencyGroup { get; }

        /// <summary>
        /// The underlying XObject dictionary.
        /// </summary>
        public PdfDictionary Dictionary { get; }

        /// <summary>
        /// The resources dictionary (/Resources) for the form, if present.
        /// </summary>
        public PdfDictionary Resources { get; }

        /// <summary>
        /// The parent page for this form.
        /// </summary>
        public PdfPage Page { get; }

        /// <summary>
        /// The original Form XObject.
        /// </summary>
        public PdfObject XObject { get; }

        /// <summary>
        /// Creates a <see cref="PdfForm"/> from a Form XObject.
        /// </summary>
        /// <param name="xObject">The Form XObject.</param>
        /// <param name="page">Parent page.</param>
        /// <returns>A parsed <see cref="PdfForm"/> instance.</returns>
        public static PdfForm FromXObject(PdfObject xObject, PdfPage page)
        {
            var dict = xObject.Dictionary;
            var matrixArray = dict.GetArray(PdfTokens.MatrixKey);
            var bboxArray = dict.GetArray(PdfTokens.BBoxKey);
            var groupDict = dict.GetDictionary(PdfTokens.GroupKey);
            var resourcesDict = dict.GetDictionary(PdfTokens.ResourcesKey);

            SKMatrix matrix = PdfLocationUtilities.CreateMatrix(matrixArray) ?? SKMatrix.Identity;
            SKRect bbox = PdfLocationUtilities.CreateBBox(bboxArray) ?? SKRect.Empty;

            PdfTransparencyGroup transparencyGroup = null;
            if (groupDict != null)
            {
                transparencyGroup = PdfSoftMaskParser.ParseTransparencyGroup(groupDict, page);
            }

            return new PdfForm(matrix, bbox, transparencyGroup, dict, resourcesDict, page, xObject);
        }

        /// <summary>
        /// Creates a <see cref="FormXObjectPageWrapper"/> for this form using the stored page and resources.
        /// </summary>
        /// <returns>A <see cref="FormXObjectPageWrapper"/> instance.</returns>
        internal FormXObjectPageWrapper GetFormPage()
        {
            return new FormXObjectPageWrapper(Page, XObject);
        }

        /// <summary>
        /// Returns the decoded form stream data as <see cref="ReadOnlyMemory{byte}"/>.
        /// </summary>
        /// <returns>The decoded form stream data.</returns>
        public ReadOnlyMemory<byte> GetFormData()
        {
            return XObject.DecodeAsMemory();
        }


        /// <summary>
        /// Gets the bounding rectangle of the object after applying the current transformation matrix.
        /// </summary>
        /// <returns>A <see cref="SKRect"/> representing the transformed bounding rectangle.</returns>
        public SKRect GetTransformedBounds()
        {
            return Matrix.MapRect(BBox);
        }
    }
}
