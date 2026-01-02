using Microsoft.Extensions.Logging;
using PdfReader.Color.Paint;
using PdfReader.Forms;
using PdfReader.Parsing;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Utilities;
using SkiaSharp;

namespace PdfReader.Rendering.Form;

/// <summary>
/// Standard implementation of Form renderer for rendering PDF Form XObjects.
/// </summary>
public class FormRenderer : IFormRenderer
{
    private readonly IPdfRenderer _renderer;
    private readonly ILoggerFactory _loggerFactory;

    public FormRenderer(IPdfRenderer renderer, ILoggerFactory loggerFactory)
    {
        _renderer = renderer;
        _loggerFactory = loggerFactory;
    }

    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        uint objectNumber = formXObject.XObject.Reference.ObjectNumber;

        if (graphicsState.RecursionGuard.Contains(objectNumber))
        {
            return;
        }

        graphicsState.RecursionGuard.Add(objectNumber);

        using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, graphicsState);
        softMaskScope.BeginDrawContent();

        int count = canvas.Save();

        // Apply form matrix if present
        canvas.Concat(formXObject.Matrix);

        // Clip to /BBox
        canvas.ClipRect(formXObject.BBox, antialias: !graphicsState.RenderingParameters.PreviewMode);

        if (formXObject.TransparencyGroup != null)
        {
            using var formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);
            canvas.SaveLayer(formXObject.BBox, formPaint);
        }

        // Decode and render content with a cloned state that clears parent soft mask
        var content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            var parseContext = new PdfParseContext(content);
            var formPage = formXObject.GetFormPage();

            var localGs = new PdfGraphicsState(formPage, graphicsState.RecursionGuard, graphicsState.RenderingParameters, graphicsState.ExternalTransferFunction);
            localGs.CTM = formXObject.Matrix;

            var renderer = new PdfContentStreamRenderer(_renderer, formPage);
            renderer.RenderContext(canvas, ref parseContext, localGs);
        }


        canvas.RestoreToCount(count);

        softMaskScope.EndDrawContent();


        graphicsState.RecursionGuard.Remove(objectNumber);
    }

    //public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState)
    //{
    //    uint objectNumber = formXObject.XObject.Reference.ObjectNumber;

    //    if (graphicsState.RecursionGuard.Contains(objectNumber))
    //    {
    //        return;
    //    }

    //    graphicsState.RecursionGuard.Add(objectNumber);

    //    // Record the form content into a picture first
    //    using var recorder = new SKPictureRecorder();
    //    using var recCanvas = recorder.BeginRecording(formXObject.BBox);

    //    var localGs = new PdfGraphicsState(graphicsState.Page, graphicsState.RecursionGuard, graphicsState.RenderingParameters);
    //    localGs.CTM = formXObject.Matrix;

    //    // Render content to the recording canvas
    //    var content = formXObject.GetFormData();
    //    if (!content.IsEmpty)
    //    {
    //        var parseContext = new PdfParseContext(content);
    //        var formPage = formXObject.GetFormPage();

    //        var renderer = new PdfContentStreamRenderer(_renderer, formPage);
    //        renderer.RenderContext(recCanvas, ref parseContext, localGs);
    //    }

    //    // End recording and get the picture
    //    using var picture = recorder.EndRecording();

    //    // Now draw the recorded picture to the actual canvas
    //    canvas.Save();

    //    // Apply form matrix if present
    //    canvas.Concat(formXObject.Matrix);

    //    using var softMaskScope = new SoftMaskDrawingScope(_renderer, canvas, graphicsState);
    //    softMaskScope.BeginDrawContent();

    //    if (formXObject.TransparencyGroup != null)
    //    {
    //        using var formPaint = PdfPaintFactory.CreateCompositionLayerPaint(graphicsState);
    //       canvas.SaveLayer(formXObject.BBox, formPaint);
    //        canvas.DrawPicture(picture);
    //        canvas.Restore();
    //    }
    //    else
    //    {
    //        canvas.DrawPicture(picture);
    //    }

    //    softMaskScope.EndDrawContent();

    //    canvas.Restore();

    //    graphicsState.RecursionGuard.Remove(objectNumber);
    //}
}
