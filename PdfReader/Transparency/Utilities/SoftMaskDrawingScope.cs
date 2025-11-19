using System;
using System.Collections.Generic;
using PdfReader.Color.Paint;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using PdfReader.Transparency.Model;
using SkiaSharp;

namespace PdfReader.Transparency.Utilities;

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

        var layerPaint = PdfPaintFactory.CreateLayerPaint(_graphicsState);
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

        try
        {
            // Record the soft mask content into a picture.
            using var recorder = new SKPictureRecorder();
            using var recCanvas = recorder.BeginRecording(_softMask.MaskForm.GetTransformedBounds());

            recCanvas.Save();

            recCanvas.Concat(_softMask.MaskForm.Matrix);

            // Background for luminosity masks (BC in group color space).
            if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
            {
                var backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent);
                using var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);
                recCanvas.DrawRect(_softMask.MaskForm.BBox, backgroundPaint);
            }

            // Render mask content stream.
            var contentData = _softMask.MaskForm.GetFormData();
            if (!contentData.IsEmpty)
            {
                var parseContext = new PdfParseContext(contentData);
                var maskGs = _softMask.Subtype == PdfSoftMaskSubtype.Luminosity
                    ? SoftMaskUtilities.CreateLuminosityMaskGraphicsState()
                    : SoftMaskUtilities.CreateAlphaMaskGraphicsState();

                var page = _softMask.MaskForm.GetFormPage();
                var contentRenderer = new PdfContentStreamRenderer(_renderer, page);
                contentRenderer.RenderContext(recCanvas, ref parseContext, maskGs, new HashSet<int>());
            }

            recCanvas.Restore();

            using var picture = recorder.EndRecording();
            using var maskPaint = PdfPaintFactory.CreateMaskPaint();

            using var alphaFilter = _softMask.Subtype == PdfSoftMaskSubtype.Luminosity
                ? SKColorFilter.CreateLumaColor()
                : null;

            maskPaint.ColorFilter = alphaFilter;

            _canvas.DrawPicture(picture, maskPaint);
        }
        finally
        {
            // Close the layer started in BeginDrawContent.
            _canvas.Restore();
            shouldApplyMask = false;
        }
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
            try
            {
                EndDrawContent();
            }
            catch
            {
                // Best effort; avoid throwing from Dispose.
            }
        }
    }
}
