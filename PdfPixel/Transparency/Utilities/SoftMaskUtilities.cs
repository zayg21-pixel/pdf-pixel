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
/// Provides helpers to build temporary graphics states and color filters used when interpreting /SMask dictionaries.
/// </summary>
internal static class SoftMaskUtilities
{
    /// <summary>
    /// Records the commands that draw <paramref name="softMask"/>'s form content, in the mask form's own
    /// space, and caches the recording on the document so that the operations sharing a mask replay one
    /// recording instead of each rendering the mask's content stream again. Returns null when the form
    /// carries no content, or when it is already being drawn further up the stack.
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

        // Anything already under way is suppressed wherever the mask reaches it again, which makes the
        // recording specific to this use; only a mask reached with nothing else in flight holds for
        // every other use of it.
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
    /// Create a graphics state optimized for alpha soft mask rendering (Subtype = /Alpha).
    /// We render the mask content in solid white so that the resulting luminance (or direct alpha composition)
    /// produces maximum coverage for painted marks and the per‑object alpha comes only from transparency operators
    /// (e.g. ca/CA) or explicit painting. Using white ensures that stroke/fill operations that do not explicitly
    /// change color contribute a full 1.0 channel and the eventual mask derives only from transparency semantics.
    /// </summary>
    public static PdfGraphicsState CreateAlphaMaskGraphicsState(PdfPage page, PdfGraphicsState sourceState)
    {
        return new(page, sourceState)
        {
            // White stroke/fill -> maximum channel; alpha modulation derives from transparency settings.
            StrokePaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.White, PdfPaintStyle.Fill)
        };
    }

    /// <summary>
    /// Create a graphics state optimized for luminosity soft mask rendering (Subtype = /Luminosity).
    /// For luminosity masks we keep natural grayscale intent by rendering with black (or dark) base color so that
    /// the mask result comes from actual painted content luminance (after optional color space conversions) rather
    /// than being forced to pure white. This aligns with the PDF spec where a luminosity soft mask derives its
    /// values from the luminance of the group result. Black base simplifies interpretation and avoids unintended
    /// bias toward full alpha when colors are not explicitly set.
    /// </summary>
    public static PdfGraphicsState CreateLuminosityMaskGraphicsState(PdfPage page, PdfGraphicsState sourceState)
    {
        return new(page, sourceState)
        {
            // Black stroke/fill -> preserves true luminance contribution of painted colors.
            StrokePaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Stroke),
            FillPaint = PdfPaint.Solid(PdfColors.Black, PdfPaintStyle.Fill)
        };
    }
}
