using PdfPixel.Color;
using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Transparency.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Transparency.Utilities;

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
        ReadOnlyMemory<byte> contentData = maskForm.GetFormData();

        if (contentData.IsEmpty)
        {
            return null;
        }

        PdfReference maskFormReference = maskForm.XObject.Reference;

        if (sourceState.RecursionGuard.Contains(maskFormReference.ObjectNumber))
        {
            return null;
        }

        Dictionary<PdfSoftMaskRecordingKey, PdfCommandRecorder> recordingCache = sourceState.Page.Document.ObjectCache.SoftMaskForms;
        PdfSoftMaskRecordingKey key = new(maskFormReference, worldToMaskForm);

        if (maskFormReference.IsValid && recordingCache.TryGetValue(key, out PdfCommandRecorder? cachedRecording))
        {
            return cachedRecording;
        }

        bool reachedAtTopLevel = sourceState.RecursionGuard.Count == 0;

        sourceState.RecursionGuard.Add(maskFormReference.ObjectNumber);

        FormXObjectPageWrapper maskPage = maskForm.GetFormPage();
        PdfGraphicsState maskState = (softMask.Subtype == PdfSoftMaskSubtype.Luminosity)
            ? CreateLuminosityMaskGraphicsState(maskPage, sourceState)
            : CreateAlphaMaskGraphicsState(maskPage, sourceState);

        maskState.CTM = worldToMaskForm;
        maskState.ClipBounds = worldToMaskForm.MapRect(maskForm.BBox);

        PdfCommandRecorder recorder = new();
        PdfContentStreamRenderer contentRenderer = new(renderer, maskPage);
        PdfParseContext parseContext = new(contentData);
        contentRenderer.RenderContext(recorder, ref parseContext, maskState);

        sourceState.RecursionGuard.Remove(maskFormReference.ObjectNumber);

        if (maskFormReference.IsValid && reachedAtTopLevel)
        {
            recordingCache[key] = recorder;
        }

        return recorder;
    }

    /// <summary>
    /// Creates the graphics state an alpha soft mask (Subtype = /Alpha) renders its content with,
    /// painting in solid white.
    /// </summary>
    public static PdfGraphicsState CreateAlphaMaskGraphicsState(PdfPage page, PdfGraphicsState sourceState)
    {
        return new(page, sourceState)
        {
            StrokePaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Fill)
        };
    }

    /// <summary>
    /// Creates the graphics state a luminosity soft mask (Subtype = /Luminosity) renders its content
    /// with, painting in solid black.
    /// </summary>
    public static PdfGraphicsState CreateLuminosityMaskGraphicsState(PdfPage page, PdfGraphicsState sourceState)
    {
        return new(page, sourceState)
        {
            StrokePaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Fill)
        };
    }
}
