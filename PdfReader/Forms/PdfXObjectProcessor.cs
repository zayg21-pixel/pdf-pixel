using PdfReader.Color.Paint;
using PdfReader.Imaging.Model;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Rendering.Operators;
using PdfReader.Rendering.State;
using PdfReader.Text;
using PdfReader.Transparency;
using PdfReader.Transparency.Utilities;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.Forms;

// TODO: need to implement XObject model and separate parsing and rendering. Implement separate renderer for XObjects.
public static class PdfXObjectProcessor
{
    public static void ProcessXObject(PdfString xObjectName, PdfGraphicsState graphicsState,
                                      SKCanvas canvas, PdfPage page, HashSet<int> processingXObjects)
    {
        if (xObjectName.IsEmpty)
            return;

        var xObject = GetXObjectFromResources(page, xObjectName);
        if (xObject == null)
            return;

        if (processingXObjects.Contains(xObject.Reference.ObjectNumber))
            return;

        var subtype = xObject.Dictionary.GetName(PdfTokens.SubtypeKey).AsEnum<PdfXObjectSubtype>();

        switch (subtype)
        {
            case PdfXObjectSubtype.Image:
                ProcessImageXObject(xObject, xObjectName, graphicsState, canvas, page);
                break;
            case PdfXObjectSubtype.Form:
                ProcessFormXObject(xObject, xObjectName, graphicsState, canvas, page, processingXObjects);
                break;
            default:
                break;
        }
    }

    private static PdfObject GetXObjectFromResources(PdfPage page, PdfString xObjectName)
    {
        return GetXObjectFromResourcesDict(page.ResourceDictionary, xObjectName);
    }

    private static PdfObject GetXObjectFromResourcesDict(PdfDictionary resourcesDict, PdfString xObjectName)
    {
        var xObjectDict = resourcesDict.GetDictionary(PdfTokens.XObjectKey);
        if (xObjectDict == null)
            return null;

        return xObjectDict.GetPageObject(xObjectName);
    }

    private static void ProcessImageXObject(PdfObject imageXObject, PdfString xObjectName, PdfGraphicsState graphicsState,
                                            SKCanvas canvas, PdfPage page)
    {
        var pdfImage = PdfImage.FromXObject(imageXObject, page, xObjectName, isSoftMask: false);

        page.Document.PdfRenderer.DrawUnitImage(canvas, pdfImage, graphicsState, page);
    }

    private static void ProcessFormXObject(PdfObject formXObject, PdfString xObjectName, PdfGraphicsState graphicsState,
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
            clipRect = new SKRect(bbox.GetFloat(0), bbox.GetFloat(1), bbox.GetFloat(2), bbox.GetFloat(3)).Standardized;
        }

        // Use form paint to composite the whole form with correct alpha/blend when needed
        using var formPaint = PdfPaintFactory.CreateFormXObjectPaint(graphicsState);

        // Apply form matrix if present
        canvas.Concat(transformMatrix);

        // Clip to /BBox
        if (!clipRect.IsEmpty)
        {
            canvas.ClipRect(clipRect, antialias: true);
        }

        canvas.SaveLayer(clipRect, formPaint);

        using var softMaskScope = new SoftMaskDrawingScope(canvas, graphicsState, page);
        softMaskScope.BeginDrawContent();

        try
        {
            // Decode and render content with a cloned state that clears parent soft mask
            var content = formXObject.DecodeAsMemory();
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
            canvas.Restore();

            softMaskScope.EndDrawContent();

            processingXObjects.Remove(formXObject.Reference.ObjectNumber);
        }
    }
}