using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Tagging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Transparency;

/// <summary>
/// Utility functions for soft mask processing.
/// </summary>
internal static class SoftMaskUtilities
{
    private static readonly PdfTransparencyGroupContext UndeclaredMaskGroup = new(null, isolated: true, knockout: false);

    /// <summary>
    /// Returns <paramref name="softMask"/> as it is set in <paramref name="state"/>: its group positioned in the
    /// state's current space.
    /// </summary>
    public static PdfSoftMaskSource CreateSource(PdfRenderer renderer, PdfSoftMask softMask, PdfGraphicsState state)
    {
        PdfForm maskForm = softMask.MaskForm;
        PdfMatrix worldToMaskForm = PdfMatrix.Concat(state.CTM, maskForm.Matrix);

        // TODO: The mask group is recorded at every gs that sets it. Cache the recording once its key covers the host's
        // resources when the form has none of its own, and the graphics state the mask content starts from.
        PdfCommandRecorder recorder = GetMaskFormRecording(renderer, softMask, maskForm, state, worldToMaskForm) ?? new PdfCommandRecorder();

        PdfTransparencyGroupContext groupContext = UndeclaredMaskGroup;
        PdfTransparencyGroup? group = maskForm.TransparencyGroup;

        if (group != null)
        {
            groupContext = new PdfTransparencyGroupContext(group.ResolveColorSpace(state.Host.Resources), group.Isolated, group.Knockout);
        }

        PdfGroupSource source = new(new DrawRecordingCommand(recorder, maskForm.Matrix, null), groupContext, maskForm.Matrix.MapRect(maskForm.BoundingBox), null);
        PdfPaint? backdropPaint = (softMask.Subtype == PdfSoftMaskSubtype.Luminosity) ? softMask.GetBackgroundPaint(state) : null;

        return new PdfSoftMaskSource(softMask.Subtype, source, maskForm.BoundingBox, backdropPaint, softMask.TransferFunction);
    }

    /// <summary>
    /// Records the commands that draw <paramref name="softMask"/>'s form content, in the mask form's own
    /// space. Returns <see langword="null"/> when the form carries no content, or when it is already
    /// being drawn further up the stack.
    /// </summary>
    public static PdfCommandRecorder? GetMaskFormRecording(
        PdfRenderer renderer,
        PdfSoftMask softMask,
        PdfForm maskForm,
        PdfGraphicsState sourceState,
        in PdfMatrix worldToMaskForm)
    {
        ReadOnlyMemory<byte> contentData = maskForm.Stream.DecodeAsMemory();

        if (contentData.IsEmpty)
        {
            return null;
        }

        PdfReference maskFormReference = maskForm.Reference;

        if (sourceState.RecursionGuard.Contains(maskFormReference.ObjectNumber))
        {
            return null;
        }

        sourceState.RecursionGuard.Add(maskFormReference.ObjectNumber);

        PdfContentHost maskHost = maskForm.GetFormHost(sourceState);
        PdfRectangle maskClipBounds = worldToMaskForm.MapRect(maskForm.BoundingBox);
        PdfGraphicsState maskState = (softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
            ? CreateLuminosityMaskGraphicsState(maskHost, maskClipBounds, sourceState)
            : CreateAlphaMaskGraphicsState(maskHost, maskClipBounds, sourceState);

        maskState.CTM = worldToMaskForm;

        PdfCommandRecorder recorder = new();
        PdfContentStreamRenderer contentRenderer = new(renderer, maskHost);
        PdfParseContext parseContext = new(contentData, maskHost.Document);
        PdfContentStreamScope contentStreamScope = new(PdfContentStreamType.SoftMask);
        recorder.Process(new BeginScopeCommand(contentStreamScope));
        contentRenderer.RenderContext(recorder, ref parseContext, maskState);
        recorder.Process(new EndScopeCommand());

        sourceState.RecursionGuard.Remove(maskFormReference.ObjectNumber);

        return recorder;
    }

    /// <summary>
    /// Creates the graphics state an alpha soft mask (Subtype = /Alpha) renders its content with,
    /// painting in solid white.
    /// </summary>
    public static PdfGraphicsState CreateAlphaMaskGraphicsState(IPdfContentHost host, in PdfRectangle clipBounds, PdfGraphicsState sourceState)
    {
        PdfGraphicsState state = new(host, clipBounds, sourceState);
        state.StrokePaint = PdfPaintFactory.WithGray(state.StrokePaint, PdfPaintFactory.WhiteGray);
        state.FillPaint = PdfPaintFactory.WithGray(state.FillPaint, PdfPaintFactory.WhiteGray);

        return state;
    }

    /// <summary>
    /// Creates the graphics state a luminosity soft mask (Subtype = /Luminosity) renders its content
    /// with, painting in solid black.
    /// </summary>
    public static PdfGraphicsState CreateLuminosityMaskGraphicsState(IPdfContentHost host, in PdfRectangle clipBounds, PdfGraphicsState sourceState)
        => new(host, clipBounds, sourceState);
}
