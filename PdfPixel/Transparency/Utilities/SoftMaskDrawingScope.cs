using System;
using PdfPixel.Color.Filters;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;

namespace PdfPixel.Transparency.Utilities;

/// <summary>
/// Disposable scope to render content with an optional soft mask and transparency group applied.
/// Usage:
/// using (var scope = new SoftMaskDrawingScope(canvas, graphicsState, currentPage))
/// {
///     scope.BeginDrawContent();
///     // draw page/form content here
///     scope.EndDrawContent();
/// }
/// </summary>
public sealed class SoftMaskDrawingScope : IDisposable
{
    private readonly IPdfRenderer _renderer;
    private readonly SKCanvas _canvas;
    private readonly PdfSoftMask _softMask;
    private readonly PdfGraphicsState _graphicsState;

    private bool began;
    private bool shouldApplyMask;
    private bool disposed;

    /// <summary>
    /// Create a new soft mask drawing scope.
    /// Bounds are derived internally from the soft mask transformed bounds intersected with the current canvas clip.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="canvas">Target canvas.</param>
    /// <param name="graphicsState">Current graphics state (provides the soft mask).</param>
    public SoftMaskDrawingScope(
        IPdfRenderer renderer,
        SKCanvas canvas,
        PdfGraphicsState graphicsState)
    {
        _renderer = renderer;
        _canvas = canvas;
        _softMask = graphicsState.SoftMask;
        _graphicsState = graphicsState;
    }

    /// <summary>
    /// Begins the drawing scope. If a valid soft mask is provided, creates a layer to capture content.
    /// </summary>
    public void BeginDrawContent()
    {
        if (began)
        {
            return;
        }

        began = true;

        if (_canvas == null)
        {
            return;
        }

        shouldApplyMask = _softMask != null && _softMask.MaskForm != null;

        if (!shouldApplyMask)
        {
            return;
        }

        var layerPaint = PdfPaintFactory.CreateMaskLayerPaint(_graphicsState);
        _canvas.SaveLayer(layerPaint);
    }

    /// <summary>
    /// Ends the drawing scope. When a soft mask is active, records the mask picture and applies it using DstIn.
    /// Ensures the canvas restore is called when a layer was created.
    /// </summary>
    public void EndDrawContent()
    {
        if (!began)
        {
            return;
        }

        if (!shouldApplyMask)
        {
            return;
        }

        // recording to separate picture follows 2 aims:
        // - luminocity filter is applied to pixels, that means that if mask is slightly off, filter will still be applied to picture, that eliminates artifacts on edges
        // - we can omit clipping to bbox, because picture will be drawn only inside bbox anyway

        // Record the soft mask content into a picture.
        using var recorder = new SKPictureRecorder();
        using var recCanvas = recorder.BeginRecording(_softMask.MaskForm.BBox);

        // Background for luminosity masks (BC in group color space).
        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            var backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent, _graphicsState.FullTransferFunction);
            using var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor, _graphicsState);
            recCanvas.DrawRect(_softMask.MaskForm.BBox, backgroundPaint);
        }

        // Render mask content stream.
        var contentData = _softMask.MaskForm.GetFormData();
        if (!contentData.IsEmpty)
        {
            var softMaskObjectNumber = _softMask.MaskForm.XObject.Reference.ObjectNumber;
            if (_graphicsState.RecursionGuard.Contains(softMaskObjectNumber))
            {
                // Prevent infinite recursion.
                return;
            }

            _graphicsState.RecursionGuard.Add(softMaskObjectNumber);

            var page = _softMask.MaskForm.GetFormPage();

            var parseContext = new PdfParseContext(contentData);
            var maskGs = _softMask.Subtype == PdfSoftMaskSubtype.Luminosity
                ? SoftMaskUtilities.CreateLuminosityMaskGraphicsState(page, _graphicsState)
                : SoftMaskUtilities.CreateAlphaMaskGraphicsState(page, _graphicsState);

            // Use TR from soft mask definition as external transfer function for local GS
            if (maskGs.ExternalTransferFunction == null)
            {
                maskGs.ExternalTransferFunction = _softMask.TransferFunction;
            }
            else
            {
                maskGs.ExternalTransferFunction = new ChainedColorTransform(maskGs.ExternalTransferFunction, _softMask.TransferFunction);
            }

            maskGs.CTM = SKMatrix.Concat(_graphicsState.CTM, _softMask.MaskForm.Matrix);

            var contentRenderer = new PdfContentStreamRenderer(_renderer, page);
            contentRenderer.RenderContext(recCanvas, ref parseContext, maskGs);

            _graphicsState.RecursionGuard.Remove(softMaskObjectNumber);
        }

        using var picture = recorder.EndRecording();
        using var maskPaint = PdfPaintFactory.CreateMaskPaint(_graphicsState);

        using var alphaFilter = _softMask.Subtype == PdfSoftMaskSubtype.Luminosity
            ? SKColorFilter.CreateLumaColor()
            : null;

        maskPaint.ColorFilter = alphaFilter;

        _canvas.Concat(_softMask.MaskForm.Matrix);

        _canvas.DrawPicture(picture, maskPaint);

        // Close the layer started in BeginDrawContent.
        _canvas.Restore();
        shouldApplyMask = false;
    }

    /// <summary>
    /// Dispose pattern. Attempts to safely end the scope if caller forgot to call EndDrawContent.
    /// </summary>
    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;

        // Ensure proper teardown if the caller forgot to call EndDrawContent.
        if (began && shouldApplyMask)
        {
            EndDrawContent();
        }
    }
}
