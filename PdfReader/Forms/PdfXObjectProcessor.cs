using PdfReader.Color.Paint;
using PdfReader.Imaging.Model;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using PdfReader.Text;
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

        return xObjectDict.GetObject(xObjectName);
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

        var form = PdfForm.FromXObject(formXObject, page);

        // Use form paint to composite the whole form with correct alpha/blend when needed
        using var formPaint = PdfPaintFactory.CreateFormXObjectPaint(graphicsState);

        // Apply form matrix if present
        canvas.Concat(form.Matrix);

        // Clip to /BBox
        canvas.ClipRect(form.BBox, antialias: true);
        canvas.SaveLayer(form.BBox, formPaint);

        using var softMaskScope = new SoftMaskDrawingScope(canvas, graphicsState);
        softMaskScope.BeginDrawContent();

        try
        {
            // Decode and render content with a cloned state that clears parent soft mask
            var content = form.GetFormData();
            if (!content.IsEmpty)
            {
                var parseContext = new PdfParseContext(content);
                var formPage = form.GetFormPage();
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