using PdfRender.Annotations.Models;
using PdfRender.Models;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace PdfRender.View;

/// <summary>
/// Implements the IPdfRenderer interface.
/// </summary>
internal sealed class PdfRenderer
{
    private readonly PdfDocument document;

    public PdfRenderer(PdfDocument document)
    {
        this.document = document;
    }

    public PageInfo GetPageInfo(int pageNumber)
    {
        var pdfPage = document.Pages[pageNumber - 1];
        return new PageInfo(pdfPage.CropBox.Width, pdfPage.CropBox.Height, pdfPage.Rotation);
    }

    public AnnotationPopup[] GetAnnotationPopups(int pageNumber)
    {
        if (pageNumber < 1 || pageNumber > document.Pages.Count)
        {
            return Array.Empty<AnnotationPopup>();
        }

        var pdfPage = document.Pages[pageNumber - 1];
        if (pdfPage.Annotations.Count == 0)
        {
            return Array.Empty<AnnotationPopup>();
        }

        var popups = new List<AnnotationPopup>();

        foreach (var annotation in pdfPage.Annotations)
        {
            // Skip annotations that don't have meaningful popup content
            if (annotation.Contents.IsEmpty && 
                annotation.Title.IsEmpty &&
                annotation.Subject.IsEmpty)
            {
                continue;
            }

            var messages = CreateAnnotationMessages(annotation);
            if (messages.Length > 0)
            {
                // Convert PDF coordinates to WPF coordinates
                var rect = FromPdfRect(pdfPage, annotation.Rectangle);
                popups.Add(new AnnotationPopup(messages, rect));
            }
        }

        return popups.ToArray();
    }

    /// <summary>
    /// Converts PDF rectangle coordinates to WPF coordinates.
    /// </summary>
    /// <param name="pdfPage">The PDF page for coordinate system reference.</param>
    /// <param name="pdfRect">Rectangle in PDF coordinates.</param>
    /// <returns>Rectangle in WPF coordinates.</returns>
    private static SKRect FromPdfRect(PdfPage pdfPage, SKRect pdfRect)
    {
        // PDF coordinate system: origin at bottom-left, Y increases upward
        // General coordinate system: origin at top-left, Y increases downward
        // Convert from PDF coordinates to general coordinates with proper Y-axis flip
        return new SKRect(
            pdfRect.Left - pdfPage.CropBox.Left, 
            pdfPage.CropBox.Height - (pdfRect.Bottom - pdfPage.CropBox.Top), // TODO: is this correct
            pdfRect.Width, 
            pdfRect.Height);
    }

    /// <summary>
    /// Creates annotation messages from an annotation's metadata.
    /// </summary>
    private static AnnotationMessage[] CreateAnnotationMessages(PdfAnnotationBase annotation)
    {
        var messages = new List<AnnotationMessage>();

        // Get annotation metadata from properties
        var title = annotation.Title.ToString();
        var subject = annotation.Subject.ToString();
        var contents = annotation.Contents.ToString();
        var creationDate = annotation.CreationDate;
        var modificationDate = annotation.ModificationDate;

        // Create primary message from contents
        if (!string.IsNullOrEmpty(contents)) // TODO: cleanup this all
        {
            var messageDate = creationDate ?? modificationDate;
            var messageTitle = !string.IsNullOrEmpty(title) ? title : "Annotation";
            
            messages.Add(new AnnotationMessage(
                messageDate.HasValue ? new DateTimeOffset(messageDate.Value) : null,
                messageTitle,
                contents));
        }

        // Create additional message for subject if different from contents
        if (!string.IsNullOrEmpty(subject) && subject != contents)
        {
            var messageDate = modificationDate ?? creationDate;
            
            messages.Add(new AnnotationMessage(
                messageDate.HasValue ? new DateTimeOffset(messageDate.Value) : null,
                "Subject",
                subject));
        }

        // If we have title/author but no other content, create a basic message
        if (messages.Count == 0 && !string.IsNullOrEmpty(title))
        {
            var messageDate = creationDate ?? modificationDate;
            
            messages.Add(new AnnotationMessage(
                messageDate.HasValue ? new DateTimeOffset(messageDate.Value) : null,
                "Author",
                title));
        }

        return messages.ToArray();
    }

    public SKPicture GetPicture(int pageNumber, double scale)
    {
        try
        {
            return GetPictureInternal(pageNumber, scale, previewMode: false);
        }
        catch
        {
            return null;
        }
    }

    public SKImage GetThumbnail(int pageNumber, int maxThumbnailSize)
    {
        try
        {
            var pdfPage = document.Pages[pageNumber - 1];
            var maxDimension = Math.Max(pdfPage.CropBox.Width, pdfPage.CropBox.Height);
            var scale = maxThumbnailSize / maxDimension;

            var width = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Width * scale));
            var height = (int)Math.Max(1, Math.Round(pdfPage.CropBox.Height * scale));

            var bitmapInfo = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            using var bitmap = new SKBitmap(bitmapInfo);

            using var canvas = new SKCanvas(bitmap);

            canvas.Clear(SKColors.Transparent);
            canvas.Scale((float)scale);
            canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

            var parameters = new PdfRenderingParameters
            {
                ScaleFactor = (float)scale,
                PreviewMode = true
            };

            pdfPage.Draw(canvas, parameters);
            canvas.Flush();

            return SKImage.FromBitmap(bitmap);
        }
        catch
        {
            return null;
        }
    }

    private SKPicture GetPictureInternal(int pageNumber, double scale, bool previewMode)
    {
        var pdfPage = document.Pages[pageNumber - 1];

        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(SKRect.Create(pdfPage.CropBox.Width, pdfPage.CropBox.Height));
        canvas.ClipRect(new SKRect(0, 0, pdfPage.CropBox.Width, pdfPage.CropBox.Height));

        var parameters = new PdfRenderingParameters { ScaleFactor = (float)scale, PreviewMode = false };
        pdfPage.Draw(canvas, parameters);

        canvas.Flush();
        var picture = recorder.EndRecording();

        return picture;
    }

    public void Dispose()
    {
        document.Dispose();
    }
}
