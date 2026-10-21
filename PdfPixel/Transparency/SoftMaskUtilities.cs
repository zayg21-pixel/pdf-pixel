using PdfPixel.Color;
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
    /// <summary>
    /// Records the commands that draw <paramref name="softMask"/>'s form content, in the mask form's own
    /// space. Returns <see langword="null"/> when the form carries no content, or when it is already
    /// being drawn further up the stack.
    /// </summary>
    public static PdfCommandRecorder? GetMaskFormRecording(
        IPdfRenderer renderer,
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
        PdfParseContext parseContext = new(contentData, PdfParseContext.ContentStreamValuesCapacity);
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
        return new(host, clipBounds, sourceState)
        {
            StrokePaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Fill)
        };
    }

    /// <summary>
    /// Creates the graphics state a luminosity soft mask (Subtype = /Luminosity) renders its content
    /// with, painting in solid black.
    /// </summary>
    public static PdfGraphicsState CreateLuminosityMaskGraphicsState(IPdfContentHost host, in PdfRectangle clipBounds, PdfGraphicsState sourceState)
    {
        return new(host, clipBounds, sourceState)
        {
            StrokePaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Fill)
        };
    }
}
