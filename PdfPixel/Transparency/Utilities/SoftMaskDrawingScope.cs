using System;
using PdfPixel.Color.Filters;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;

namespace PdfPixel.Transparency.Utilities;

/// <summary>
/// Disposable scope to render content with an optional soft mask and transparency group applied.
/// Usage:
/// using (var scope = new SoftMaskDrawingScope(processor, graphicsState))
/// {
///     scope.BeginDrawContent();
///     // draw page/form content here
///     scope.EndDrawContent();
/// }
/// </summary>
public sealed class SoftMaskDrawingScope : IDisposable
{
    private readonly IPdfRenderer _renderer;
    private readonly IPdfCommandProcessor _processor;
    private readonly PdfSoftMask _softMask;
    private readonly PdfGraphicsState _graphicsState;

    private SKRect _maskBounds;
    private bool _began;
    private bool _shouldApplyMask;
    private bool _disposed;

    /// <summary>
    /// Create a new soft mask drawing scope.
    /// </summary>
    /// <param name="renderer">PDF renderer instance.</param>
    /// <param name="processor">Command processor to emit drawing commands through.</param>
    /// <param name="graphicsState">Current graphics state (provides the soft mask).</param>
    public SoftMaskDrawingScope(
        IPdfRenderer renderer,
        IPdfCommandProcessor processor,
        PdfGraphicsState graphicsState)
    {
        _renderer = renderer;
        _processor = processor;
        _softMask = graphicsState.SoftMask;
        _graphicsState = graphicsState;
    }

    /// <summary>
    /// Begins the drawing scope. If a valid soft mask is provided, creates a layer to capture content.
    /// </summary>
    public void BeginDrawContent()
    {
        if (_began)
        {
            return;
        }

        _began = true;

        if (_processor == null)
        {
            return;
        }

        _shouldApplyMask = _softMask != null && _softMask.MaskForm != null;

        if (!_shouldApplyMask)
        {
            return;
        }

        // Use the mask form's transformed BBox as tight bounds for both layers.
        // Content outside the mask region is masked to transparent anyway,
        // so constraining the layer avoids a full-clip-sized GPU texture.
        _maskBounds = _softMask.MaskForm.GetTransformedBounds();

        var layerPaint = PdfPaintFactory.CreateMaskLayerPaint(_graphicsState);
        _processor.Process(new SaveLayerCommand(_maskBounds, layerPaint)); // TODO: [HIGH] BAM page 20 is broken again. And 37 either.
    }

    /// <summary>
    /// Ends the drawing scope. When a soft mask is active, opens a second layer with DstIn
    /// blend mode (and an optional luma color filter for luminosity masks), renders the mask
    /// content into it, then restores both layers so the mask composites onto the content.
    /// </summary>
    public void EndDrawContent()
    {
        if (!_began)
        {
            return;
        }

        if (!_shouldApplyMask)
        {
            return;
        }

        // Layer 2: mask content, composited with DstIn onto the content layer when restored.
        // For luminosity masks the luma color filter converts the mask RGB → alpha during flatten.
        var maskLayerPaint = PdfPaintFactory.CreateMaskPaint(_graphicsState);

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            maskLayerPaint.ColorFilter = SKColorFilter.CreateLumaColor();
        }

        _processor.Process(new SaveLayerCommand(_maskBounds, maskLayerPaint));

        // Position the mask form.
        _processor.Process(new ConcatMatrixCommand(_softMask.MaskForm.Matrix));

        // Background for luminosity masks (BC in group color space).
        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            var backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent, _graphicsState.FullTransferFunction);
            var backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor, _graphicsState);
            _processor.Process(new DrawRectCommand(backgroundPaint));
        }

        // Render mask content stream directly into the processor (inside Layer 2).
        var contentData = _softMask.MaskForm.GetFormData();
        if (!contentData.IsEmpty)
        {
            var softMaskObjectNumber = _softMask.MaskForm.XObject.Reference.ObjectNumber;
            if (_graphicsState.RecursionGuard.Contains(softMaskObjectNumber))
            {
                // Prevent infinite recursion — still need to restore both layers.
                _processor.Process(new RestoreStateCommand());
                _processor.Process(new RestoreStateCommand());
                _shouldApplyMask = false;
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
            contentRenderer.RenderContext(_processor, ref parseContext, maskGs);

            _graphicsState.RecursionGuard.Remove(softMaskObjectNumber);
        }

        // Restore Layer 2 (DstIn composites mask onto content)
        _processor.Process(new RestoreStateCommand());

        // Restore Layer 1 (composites masked content onto parent)
        _processor.Process(new RestoreStateCommand());
        _shouldApplyMask = false;
    }

    /// <summary>
    /// Dispose pattern. Attempts to safely end the scope if caller forgot to call EndDrawContent.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        // Ensure proper teardown if the caller forgot to call EndDrawContent.
        if (_began && _shouldApplyMask)
        {
            EndDrawContent();
        }
    }
}
