using Microsoft.Extensions.Logging;
using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
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

    private PdfRectangle _layerBounds;
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

        PdfMatrix inverseCtm = _graphicsState.CTM.Invert();

        _worldToMaskForm = PdfMatrix.Concat(_graphicsState.SoftMaskCTM, _softMask.MaskForm.Matrix);
        _maskMatrix = PdfMatrix.Concat(inverseCtm, _worldToMaskForm);
        // The backdrop gives the mask a value beyond the mask form too, so the masked area spans
        // everything still visible rather than just the area the mask form covers.
        _layerBounds = inverseCtm.MapRect(_graphicsState.ClipBounds);

        _processor.Process(new SaveLayerCommand(_layerBounds));
        _processor.Process(new ClipRectangleCommand(_layerBounds, PdfClipOperation.Intersect));
    }

    /// <summary>
    /// Ends the drawing scope. When a soft mask is active, opens a second layer with a mask-compositing
    /// paint (destination-in, plus the subtype-appropriate alpha conversion and transfer function -
    /// see <see cref="PdfMaskPaintParameters"/>), renders the mask content into it, then restores both
    /// layers so the mask composites onto the content.
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

        PdfCommandRecorder recorder = new();
        recorder.Process(SaveStateCommand.Instance);

        if (_softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
        {
            PdfColor backgroundColor = _softMask.GetBackgroundColor(_graphicsState.RenderingIntent, _graphicsState.FullTransferFunction);
            PdfPaint backgroundPaint = PdfPaintFactory.CreateBackgroundPaint(backgroundColor);

            PdfPathBuilder rectPath = new();
            rectPath.AddRect(_layerBounds);

            recorder.Process(new DrawPathCommand(rectPath.ToPath(), backgroundPaint));
        }

        recorder.Process(new ConcatMatrixCommand(_maskMatrix));
        recorder.Process(new ClipRectangleCommand(_softMask.MaskForm.BBox, PdfClipOperation.Intersect));

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

            maskGs.CTM = _worldToMaskForm;
            maskGs.ClipBounds = maskGs.CTM.MapRect(_softMask.MaskForm.BBox);

            PdfContentStreamRenderer contentRenderer = new(_renderer, page);
            contentRenderer.RenderContext(recorder, ref parseContext, maskGs);

            _graphicsState.RecursionGuard.Remove(softMaskObjectNumber);
        }

        recorder.Process(RestoreStateCommand.Instance);

        PdfPaint maskPaint = PdfPaintFactory.CreateSoftMaskPaint(_softMask.Subtype, _softMask.TransferFunction);

        // Position the mask form
        _processor.Process(new SaveLayerCommand(_layerBounds, maskPaint));

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
