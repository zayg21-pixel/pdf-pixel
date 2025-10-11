using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering.Advanced;
using PdfReader.Rendering.Image;
using PdfReader.Rendering.State;
using PdfReader.Streams;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Rendering
{
    public static class PdfXObjectProcessor
    {
        public static void ProcessXObject(string xObjectName, PdfGraphicsState graphicsState,
                                          SKCanvas canvas, PdfPage page, HashSet<int> processingXObjects)
        {
            if (string.IsNullOrEmpty(xObjectName))
                return;

            var xObject = GetXObjectFromResources(page, xObjectName);
            if (xObject == null)
                return;

            if (processingXObjects.Contains(xObject.Reference.ObjectNumber))
                return;

            var subtype = xObject.Dictionary.GetName(PdfTokens.SubtypeKey);
            switch (subtype)
            {
                case PdfTokens.ImageSubtype:
                    ProcessImageXObject(xObject, xObjectName, graphicsState, canvas, page);
                    break;
                case PdfTokens.FormSubtype:
                    ProcessFormXObject(xObject, xObjectName, graphicsState, canvas, page, processingXObjects);
                    break;
                default:
                    break;
            }
        }

        private static PdfObject GetXObjectFromResources(PdfPage page, string xObjectName)
        {
            return GetXObjectFromResourcesDict(page.ResourceDictionary, xObjectName);
        }

        private static PdfObject GetXObjectFromResourcesDict(PdfDictionary resourcesDict, string xObjectName)
        {
            var xObjectDict = resourcesDict.GetDictionary(PdfTokens.XObjectKey);
            if (xObjectDict == null)
                return null;

            return xObjectDict.GetPageObject(xObjectName);
        }

        private static void ProcessImageXObject(PdfObject imageXObject, string xObjectName, PdfGraphicsState graphicsState,
                                                SKCanvas canvas, PdfPage page)
        {
            var pdfImage = PdfImage.FromXObject(imageXObject, page, xObjectName, isSoftMask: false);
            page.Document.PdfRenderer.DrawUnitImage(canvas, pdfImage, graphicsState, page);
        }

        private static void ProcessFormXObject(PdfObject formXObject, string xObjectName, PdfGraphicsState graphicsState,
                                               SKCanvas canvas, PdfPage page, HashSet<int> processingXObjects)
        {
            processingXObjects.Add(formXObject.Reference.ObjectNumber);

            // Compute tight layer bounds from /BBox and /Matrix (for mask/group save layers)
            var bbox = formXObject.Dictionary.GetArray(PdfTokens.BBoxKey);
            var matrixArray = formXObject.Dictionary.GetArray(PdfTokens.MatrixKey);

            SKMatrix transformMatrix = PdfMatrixUtilities.CreateMatrix(matrixArray);
            SKRect clipRect = SKRect.Empty;

            if (bbox != null && bbox.Count >= 4)
            {
                clipRect = new SKRect(bbox.GetFloat(0), bbox.GetFloat(1), bbox.GetFloat(2), bbox.GetFloat(3));
            }

            // Use form paint to composite the whole form with correct alpha/blend when needed
            using var formPaint = PdfPaintFactory.CreateFormXObjectPaint(graphicsState, page);

            canvas.SaveLayer(clipRect, formPaint);

            // Apply form matrix if present
            canvas.Concat(transformMatrix);

            // Clip to /BBox
            if (!clipRect.IsEmpty)
            {
                canvas.ClipRect(clipRect);
            }

            using var softMaskScope = new SoftMaskDrawingScope(canvas, graphicsState, page);
            softMaskScope.BeginDrawContent();

            // Parse /Group and apply transparency group
            PdfTransparencyGroup group = null;
            bool groupApplied = false;
            var prevGroup = graphicsState.TransparencyGroup;
            try
            {
                PdfDictionary groupDict = formXObject.Dictionary.GetDictionary(PdfTokens.GroupKey);
                if (groupDict != null)
                {
                    group = PdfGraphicsStateParser.ParseTransparencyGroup(groupDict, page);
                    graphicsState.TransparencyGroup = group;
                    if (PdfTransparencyGroupProcessor.ShouldApplyTransparencyGroup(group))
                    {
                        PdfTransparencyGroupProcessor.TryApplyTransparencyGroup(canvas, group, graphicsState);
                        groupApplied = true;
                    }
                }

                // Decode and render content with a cloned state that clears parent soft mask
                var content = PdfStreamDecoder.DecodeContentStream(formXObject);
                if (!content.IsEmpty)
                {
                    var parseContext = new PdfParseContext(content);
                    var formPage = new FormXObjectPageWrapper(page, formXObject);
                    var localGs = graphicsState.Clone();
                    // Prevent double-application: global soft mask is applied by outer wrapper
                    localGs.SoftMask = null;
                    var renderer = new PdfContentStreamRenderer(formPage);
                    renderer.RenderContext(canvas, ref parseContext, localGs, processingXObjects);
                }
            }
            finally
            {
                if (groupApplied)
                {
                    PdfTransparencyGroupProcessor.TryEndTransparencyGroup(canvas, group);
                }
                graphicsState.TransparencyGroup = prevGroup;
                canvas.Restore();

                softMaskScope.EndDrawContent();

                processingXObjects.Remove(formXObject.Reference.ObjectNumber);
            }
        }
    }
}