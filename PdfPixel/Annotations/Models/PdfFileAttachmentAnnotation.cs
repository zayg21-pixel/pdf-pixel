using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;
using System.IO;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Represents a PDF file attachment annotation.
/// </summary>
/// <remarks>
/// File attachment annotations (FileAttachment) reference a file specification (Filespec) which
/// contains an embedded file stream in the /EF dictionary. This class exposes basic metadata
/// about the attached file and provides a minimal fallback rendering (paperclip icon + name).
/// </remarks>
public class PdfFileAttachmentAnnotation : PdfAnnotationBase
{
    public PdfFileAttachmentAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FileAttachment)
    {
        // Filespec can be in the /FS entry (PDF spec) or in the /F string key for older usage.
        FileSpec = annotationObject.Dictionary.GetDictionary(PdfTokens.FSKey) ?? annotationObject.Dictionary.GetDictionary(PdfTokens.FKey);

        if (FileSpec != null)
        {
            FileName = FileSpec.GetString(PdfTokens.FKey);

            // Embedded file dictionary is in /EF with key /F or /UF. Try both.
            var efDict = FileSpec.GetDictionary(PdfTokens.EFKey);
            if (efDict != null)
            {
                EmbeddedFileObject = efDict.GetObject(PdfTokens.FKey) ?? efDict.GetObject(PdfTokens.UFKey);
            }

            // Alternatively some filespecs place the file stream directly in the Filespec as /EF
            EmbeddedFileObject ??= FileSpec.GetObject(PdfTokens.EFKey);
        }

        var nameValue = annotationObject.Dictionary.GetName(PdfTokens.NameKey);
        Icon = nameValue.AsEnum<PdfFileAttachmentIcon>();

        // TODO: complete FileSpec object parsing
    }

    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// The filespec dictionary describing the attached file.
    /// </summary>
    public PdfDictionary FileSpec { get; }

    /// <summary>
    /// The icon type that should be used to display this file attachment.
    /// </summary>
    public PdfFileAttachmentIcon Icon { get; }

    /// <summary>
    /// The original file name of the attached file, if present.
    /// </summary>
    public PdfString FileName { get; }

    /// <summary>
    /// The PDF object that contains the embedded file stream, if available.
    /// </summary>
    public PdfObject EmbeddedFileObject { get; }

    /// <summary>
    /// Creates a simple fallback rendering showing a paperclip icon and file name.
    /// </summary>
    public override SKPicture CreateFallbackRender(PdfPage page, PdfAnnotationVisualStateKind visualStateKind)
    {
        using var recorder = new SKPictureRecorder();
        using var canvas = recorder.BeginRecording(Rectangle);

        var color = ResolveColor(page, SKColors.DarkSlateGray);

        canvas.Save();
        try
        {
            // Flip 180 degrees around center to account for PDF coordinate system
            canvas.Translate(Rectangle.MidX, Rectangle.MidY);
            canvas.RotateDegrees(180);
            canvas.Translate(-Rectangle.MidX, -Rectangle.MidY);

            var inset = Math.Min(Rectangle.Width, Rectangle.Height) * 0.15f;
            var r = new SKRect(Rectangle.Left + inset, Rectangle.Top + inset, Rectangle.Right - inset, Rectangle.Bottom - inset);

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(1f, Math.Min(r.Width, r.Height) * 0.07f),
                Color = color,
                IsAntialias = true,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round
            };

            // Use the parsed icon enum (default to PushPin if unspecified)
            var iconToDraw = Icon == PdfFileAttachmentIcon.Unknown ? PdfFileAttachmentIcon.PushPin : Icon;

            switch (iconToDraw)
            {
                case PdfFileAttachmentIcon.Paperclip:


                    // Draw paperclip icon scaled to annotation rectangle
                    var path = new SKPath();
                    float ScaleX(float x) { return r.Left + x * r.Width; }
                    float ScaleY(float y) { return r.Top + y * r.Height; }

                    // Paperclip parameters (unit square)
                    const float centerX = 0.5f;
                    const float outerRadius = 0.35f;
                    const float innerRadius = 0.22f;
                    float leftOuter = centerX - outerRadius;
                    float rightOuter = centerX + outerRadius;
                    float leftInner = centerX - innerRadius;
                    float rightInner = centerX + innerRadius;

                    // Build path
                    path.MoveTo(ScaleX(rightInner), ScaleY(0.2f));
                    path.LineTo(ScaleX(rightInner), ScaleY(0.65f));
                    path.ArcTo(new SKRect(ScaleX(leftInner), ScaleY(0.55f), ScaleX(rightInner), ScaleY(0.85f)), 0, 180, false);
                    path.LineTo(ScaleX(leftInner), ScaleY(0.2f));
                    path.ArcTo(new SKRect(ScaleX(leftInner), ScaleY(0.0f), ScaleX(rightOuter), ScaleY(0.45f)), 180, 180, false);
                    path.LineTo(ScaleX(rightOuter), ScaleY(0.65f));
                    path.ArcTo(new SKRect(ScaleX(leftOuter), ScaleY(0.45f), ScaleX(rightOuter), ScaleY(0.95f)), 0, 180, false);
                    path.LineTo(ScaleX(leftOuter), ScaleY(0.1f));

                    canvas.DrawPath(path, paint);
                    break;

                case PdfFileAttachmentIcon.PushPin:
                    // Draw a simple pushpin: head + shaft
                    using (var fill = new SKPaint { Style = SKPaintStyle.Fill, Color = color.WithAlpha(180), IsAntialias = true })
                    {
                        var head = new SKRect(r.Left, r.Top, r.Right, r.Top + r.Height * 0.45f);
                        canvas.DrawRoundRect(head, 2, 2, fill);
                    }
                    canvas.DrawLine(r.MidX, r.Top + r.Height * 0.45f, r.MidX, r.Bottom, paint);
                    break;

                case PdfFileAttachmentIcon.Graph:
                    // Small bar chart icon
                    var barWidth = r.Width / 6f;
                    for (int i = 0; i < 3; i++)
                    {
                        var bx = r.Left + i * (barWidth * 2f);
                        var bh = r.Height * (0.3f + i * 0.25f);
                        var by = r.Bottom - bh;
                        var barRect = new SKRect(bx, by, bx + barWidth, r.Bottom);
                        canvas.DrawRect(barRect, paint);
                    }
                    break;

                case PdfFileAttachmentIcon.Tag:
                    // Draw a tag shape (diamond with hole)
                    var tagPath = new SKPath();
                    tagPath.MoveTo(r.Left + r.Width * 0.1f, r.Top + r.Height * 0.5f);
                    tagPath.LineTo(r.Left + r.Width * 0.5f, r.Top + r.Height * 0.1f);
                    tagPath.LineTo(r.Right - r.Width * 0.1f, r.Top + r.Height * 0.5f);
                    tagPath.LineTo(r.Left + r.Width * 0.5f, r.Bottom - r.Height * 0.1f);
                    tagPath.Close();
                    canvas.DrawPath(tagPath, paint);
                    break;
            }

            // Draw filename text if available
            if (!FileName.IsEmpty)
            {
                using var font = new SKFont(SKTypeface.Default, Math.Max(8, r.Height * 0.18f));
                using var textPaint = new SKPaint { Color = color, IsAntialias = true };
                var text = FileName.ToString();
                var x = r.Left + 2;
                var y = r.Bottom - 2;
                canvas.DrawText(text, x, y, font, textPaint);
            }
        }
        finally
        {
            canvas.Restore();
        }

        return recorder.EndRecording();
    }

    public override string ToString()
    {
        if (!FileName.IsEmpty)
        {
            return $"FileAttachment: {FileName}";
        }

        return "FileAttachment";
    }
}
