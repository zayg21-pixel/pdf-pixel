using PdfPixel.Commands;
using PdfPixel.Models;
using PdfPixel.Text;
using SkiaSharp;
using System;

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
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfFileAttachmentAnnotation"/> class.
    /// </summary>
    /// <param name="annotationObject">The PDF object representing this file attachment annotation.</param>
    public PdfFileAttachmentAnnotation(PdfObject annotationObject)
        : base(annotationObject, PdfAnnotationSubType.FileAttachment)
    {
        // Filespec can be in the /FS entry (PDF spec) or in the /F string key for older usage.
        FileSpec = annotationObject.Dictionary.GetDictionary(PdfTokens.FSKey) ?? annotationObject.Dictionary.GetDictionary(PdfTokens.FKey);

        if (FileSpec != null)
        {
            FileName = FileSpec.GetString(PdfTokens.FKey);

            // Embedded file dictionary is in /EF with key /F or /UF. Try both.
            PdfDictionary? efDict = FileSpec.GetDictionary(PdfTokens.EFKey);
            if (efDict != null)
            {
                EmbeddedFileObject = efDict.GetObject(PdfTokens.FKey) ?? efDict.GetObject(PdfTokens.UFKey);
            }

            // Alternatively some filespecs place the file stream directly in the Filespec as /EF
            EmbeddedFileObject ??= FileSpec.GetObject(PdfTokens.EFKey);
        }

        PdfString nameValue = annotationObject.Dictionary.GetName(PdfTokens.NameKey);
        Icon = nameValue.AsEnum<PdfFileAttachmentIcon>();

        // TODO: [LOW] complete FileSpec object parsing
    }

    /// <inheritdoc/>
    public override bool ShouldDisplayBubble => false;

    /// <summary>
    /// The filespec dictionary describing the attached file.
    /// </summary>
    public PdfDictionary? FileSpec { get; }

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
    public PdfObject? EmbeddedFileObject { get; }

    internal override bool RenderFallback(IPdfCommandProcessor processor, IPdfPageInternal page, PdfAnnotationVisualStateKind visualStateKind)
    {
        SKColor color = ResolveColor(page, SKColors.DarkSlateGray);

        processor.Process(new SaveStateCommand());
        try
        {
            // Flip 180 degrees around center to account for PDF coordinate system
            processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(Rectangle.MidX, Rectangle.MidY)));
            processor.Process(new ConcatMatrixCommand(SKMatrix.CreateRotationDegrees(180)));
            processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(-Rectangle.MidX, -Rectangle.MidY)));

            float inset = Math.Min(Rectangle.Width, Rectangle.Height) * 0.15f;
            SKRect r = new(Rectangle.Left + inset, Rectangle.Top + inset, Rectangle.Right - inset, Rectangle.Bottom - inset);

            float strokeWidth = Math.Max(1f, Math.Min(r.Width, r.Height) * 0.07f);

            // Use the parsed icon enum (default to PushPin if unspecified)
            PdfFileAttachmentIcon iconToDraw = (Icon == PdfFileAttachmentIcon.Unknown) ? PdfFileAttachmentIcon.PushPin : Icon;

            switch (iconToDraw)
            {
                case PdfFileAttachmentIcon.Paperclip:
                    {
                        // TODO: [HIGH] for all graphics we shall use pre-defined resources
                        // Draw paperclip icon scaled to annotation rectangle
                        using (SKPath path = new())
                        {
                            float ScaleX(float x) => r.Left + (x * r.Width);
                            float ScaleY(float y) => r.Top + (y * r.Height);

                            // Paperclip parameters (unit square)
                            const float centerX = 0.5f;
                            const float outerRadius = 0.35f;
                            const float innerRadius = 0.22f;
                            const float leftOuter = centerX - outerRadius;
                            const float rightOuter = centerX + outerRadius;
                            const float leftInner = centerX - innerRadius;
                            const float rightInner = centerX + innerRadius;

                            // Build path
                            path.MoveTo(ScaleX(rightInner), ScaleY(0.2f));
                            path.LineTo(ScaleX(rightInner), ScaleY(0.65f));
                            path.ArcTo(new SKRect(ScaleX(leftInner), ScaleY(0.55f), ScaleX(rightInner), ScaleY(0.85f)), 0, 180, false);
                            path.LineTo(ScaleX(leftInner), ScaleY(0.2f));
                            path.ArcTo(new SKRect(ScaleX(leftInner), ScaleY(0.0f), ScaleX(rightOuter), ScaleY(0.45f)), 180, 180, false);
                            path.LineTo(ScaleX(rightOuter), ScaleY(0.65f));
                            path.ArcTo(new SKRect(ScaleX(leftOuter), ScaleY(0.45f), ScaleX(rightOuter), ScaleY(0.95f)), 0, 180, false);
                            path.LineTo(ScaleX(leftOuter), ScaleY(0.1f));

                            SKPaint paint = new()
                            {
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = strokeWidth,
                                Color = color,
                                StrokeCap = SKStrokeCap.Round,
                                StrokeJoin = SKStrokeJoin.Round
                            };
                            processor.Process(new DrawPathCommand(path, paint));
                        }

                        break;
                    }
                case PdfFileAttachmentIcon.PushPin:
                    {
                        // Draw a simple pushpin: head + shaft
                        SKRect headRect = new(r.Left, r.Top, r.Right, r.Top + (r.Height * 0.45f));
                        SKPaint fillPaint = new() { Style = SKPaintStyle.Fill, Color = color.WithAlpha(180) };
                        using (SKPath headPath = new())
                        {
                            headPath.AddRoundRect(headRect, 2, 2);
                            processor.Process(new DrawPathCommand(headPath, fillPaint));
                        }

                        SKPaint shaftPaint = new()
                        {
                            Style = SKPaintStyle.Stroke,
                            StrokeWidth = strokeWidth,
                            Color = color,
                            StrokeCap = SKStrokeCap.Round,
                            StrokeJoin = SKStrokeJoin.Round
                        };
                        using (SKPath shaftPath = new())
                        {
                            shaftPath.MoveTo(r.MidX, r.Top + (r.Height * 0.45f));
                            shaftPath.LineTo(r.MidX, r.Bottom);
                            processor.Process(new DrawPathCommand(shaftPath, shaftPaint));
                        }

                        break;
                    }
                case PdfFileAttachmentIcon.Graph:
                    {
                        // Small bar chart icon
                        float barWidth = r.Width / 6f;
                        for (int i = 0; i < 3; i++)
                        {
                            float bx = r.Left + (i * (barWidth * 2f));
                            float bh = r.Height * (0.3f + (i * 0.25f));
                            float by = r.Bottom - bh;
                            SKRect barRect = new(bx, by, bx + barWidth, r.Bottom);
                            SKPaint barPaint = new()
                            {
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = strokeWidth,
                                Color = color,
                                StrokeCap = SKStrokeCap.Round,
                                StrokeJoin = SKStrokeJoin.Round
                            };
                            using SKPath barPath = new();
                            barPath.AddRect(barRect);
                            processor.Process(new DrawPathCommand(barPath, barPaint));
                        }

                        break;
                    }
                case PdfFileAttachmentIcon.Tag:
                    {
                        // Draw a tag shape (diamond with hole)
                        using (SKPath tagPath = new())
                        {
                            tagPath.MoveTo(r.Left + (r.Width * 0.1f), r.Top + (r.Height * 0.5f));
                            tagPath.LineTo(r.Left + (r.Width * 0.5f), r.Top + (r.Height * 0.1f));
                            tagPath.LineTo(r.Right - (r.Width * 0.1f), r.Top + (r.Height * 0.5f));
                            tagPath.LineTo(r.Left + (r.Width * 0.5f), r.Bottom - (r.Height * 0.1f));
                            tagPath.Close();
                            SKPaint tagPaint = new()
                            {
                                Style = SKPaintStyle.Stroke,
                                StrokeWidth = strokeWidth,
                                Color = color,
                                StrokeCap = SKStrokeCap.Round,
                                StrokeJoin = SKStrokeJoin.Round
                            };
                            processor.Process(new DrawPathCommand(tagPath, tagPaint));
                        }

                        break;
                    }
            }

            // Draw filename text if available
            if (!FileName.IsEmpty)
            {
                using SKFont font = new(SKTypeface.Default, Math.Max(8, r.Height * 0.18f));
                SKPaint textPaint = new() { Color = color };
                string text = FileName.ToString();
                float x = r.Left + 2;
                float y = r.Bottom - 2;
                SKTextBlob? blob = SKTextBlob.Create(text, font);
                processor.Process(new SaveStateCommand());
                processor.Process(new ConcatMatrixCommand(SKMatrix.CreateTranslation(x, y)));
                processor.Process(new DrawTextBlobCommand(blob, textPaint));
                processor.Process(new RestoreStateCommand());
            }
        }
        finally
        {
            processor.Process(new RestoreStateCommand());
        }

        return true;
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        if (!FileName.IsEmpty)
        {
            return $"FileAttachment: {FileName}";
        }

        return "FileAttachment";
    }
}
