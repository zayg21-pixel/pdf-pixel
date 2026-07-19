using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using SkiaSharp;
using System;

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
    private readonly PdfSoftMask? _softMask;
    private readonly PdfGraphicsState _graphicsState;

    private PdfRectangle _maskBounds;
    private PdfMatrix _maskMatrix;
    private PdfMatrix _worldToMaskForm;
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
        if (graphicsState == null)
        {
            throw new ArgumentNullException(nameof(graphicsState));
        }

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

        _shouldApplyMask = true;

        if (_softMask == null || _softMask.MaskForm == null)
        {
            _shouldApplyMask = false;
            return;
        }

        _worldToMaskForm = PdfMatrix.Concat(_graphicsState.SoftMaskCTM, _softMask.MaskForm.Matrix);
        _maskMatrix = PdfMatrix.Concat(_graphicsState.CTM.Invert(), _worldToMaskForm);
        _maskBounds = _maskMatrix.MapRect(_softMask.MaskForm.BBox);

        SKPaint layerPaint = PdfPaintFactory.CreateMaskLayerPaint();
        _processor.Process(new SaveLayerCommand(_maskBounds, layerPaint));
        _processor.Process(new ClipRectangleCommand(_maskBounds, PdfClipOperation.Intersect));
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

        if (!_shouldApplyMask || _softMask?.MaskForm == null)
        {
            return;
        }

        PdfCommandRecorder recorder = new(_graphicsState.Page.Document.LoggerFactory.CreateLogger<PdfCommandRecorder>());
        recorder.Process(SaveStateCommand.Instance);
        recorder.Process(new ConcatMatrixCommand(_maskMatrix));

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            PdfColor backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent, _graphicsState.FullTransferFunction);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            using SKPathBuilder rectPathBuilder = new();
            rectPathBuilder.AddRect(_softMask.MaskForm.BBox.ToSkRect());

            recorder.Process(new DrawPathCommand(rectPathBuilder.Detach(), backgroundPaint));
        }

        // Render mask content stream into the recorder (isolated from canvas transforms).
        ReadOnlyMemory<byte> contentData = _softMask.MaskForm.GetFormData();
        uint softMaskObjectNumber = _softMask.MaskForm.XObject.Reference.ObjectNumber;
        bool isRecursive = _graphicsState.RecursionGuard.Contains(softMaskObjectNumber);

        if (!contentData.IsEmpty && !isRecursive)
        {
            _graphicsState.RecursionGuard.Add(softMaskObjectNumber);

            Forms.FormXObjectPageWrapper page = _softMask.MaskForm.GetFormPage();

            PdfParseContext parseContext = new(contentData);
            PdfGraphicsState maskGs = (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
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

            maskGs.CTM = _worldToMaskForm;

            PdfContentStreamRenderer contentRenderer = new(_renderer, page);
            contentRenderer.RenderContext(recorder, ref parseContext, maskGs);

            _graphicsState.RecursionGuard.Remove(softMaskObjectNumber);
        }

        recorder.Process(RestoreStateCommand.Instance);

        SKPaint maskPaint = PdfPaintFactory.CreateMaskPaint();

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            maskPaint.ColorFilter = SKColorFilter.CreateLumaColor();
        }

        // Position the mask form
        _processor.Process(new SaveLayerCommand(_maskBounds, maskPaint));

        _processor.Process(new DrawRecordingCommand(recorder));
        _processor.Process(RestoreLayerCommand.Instance);

        // Restore Layer 1 (composites masked content onto parent)
        _processor.Process(RestoreLayerCommand.Instance);
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
