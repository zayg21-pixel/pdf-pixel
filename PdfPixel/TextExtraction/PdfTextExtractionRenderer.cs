using PdfPixel.Color.Paint;
using PdfPixel.Commands;
using PdfPixel.Fonts.Model;
using PdfPixel.Forms;
using PdfPixel.Imaging.Model;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Rendering.Text;
using PdfPixel.Shading.Model;
using PdfPixel.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfPixel.TextExtraction;

internal class PdfTextExtractionRenderer : IPdfRenderer
{
    public List<PdfCharacter> PageCharacters { get; } = [];

    public void DrawForm(IPdfCommandProcessor processor, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        processor.Process(new SaveStateCommand());

        // Apply form matrix
        processor.Process(new ConcatMatrixCommand(formXObject.Matrix));

        processor.Process(new ClipPathCommand(formXObject.BBox, SKClipOperation.Intersect));

        // Decode and render content with a cloned state
        System.ReadOnlyMemory<byte> content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            PdfParseContext parseContext = new(content);
            FormXObjectPageWrapper formPage = formXObject.GetFormPage();
            PdfGraphicsState localGs = new(formPage, graphicsState);
            localGs.CTM = formXObject.Matrix;

            PdfContentStreamRenderer renderer = new(this, formPage);
            renderer.RenderContext(processor, ref parseContext, localGs);
        }

        processor.Process(new RestoreStateCommand());
    }

    public void DrawImage(IPdfCommandProcessor processor, PdfImage pdfImage, PdfGraphicsState state)
    {
        // no op
    }

    public void DrawPath(IPdfCommandProcessor processor, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        // no op
    }

    public void DrawShading(IPdfCommandProcessor processor, PdfShading shading, PdfGraphicsState state)
    {
        // no op
    }

    public SKSize DrawTextSequence(IPdfCommandProcessor processor, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        SKMatrix currentMatrix = SKMatrix.Concat(processor.TotalMatrix, TextRenderUtilities.GetFullTextMatrix(state));
        float advance = 0;
        int i = 0;
        while (i < glyphs.Count)
        {
            SKTypeface groupTypeface = glyphs[i].CharacterInfo.Typeface;
            float groupScale = glyphs[i].Scale;
            int j = i;
            // Find run of glyphs with same typeface and scale
            while (j < glyphs.Count && glyphs[j].CharacterInfo.Typeface == groupTypeface && glyphs[j].Scale == groupScale)
            {
                j++;
            }

            using (SKFont skFont = PdfPaintFactory.CreateTextFont(groupTypeface))
            {
                skFont.ScaleX = groupScale;
                SKFontMetrics metrics = skFont.Metrics;
                float top = metrics.Ascent;
                float bottom = metrics.Descent;

                for (int k = i; k < j; k++)
                {
                    ShapedGlyph glyph = glyphs[k];
                    SKRect rect = new(advance, top, advance + glyph.CharacterInfo.Advancement, bottom);
                    rect = currentMatrix.MapRect(rect).Standardized;
                    if (rect.Width != 0)
                    {
                        PageCharacters.Add(new PdfCharacter(glyph.CharacterInfo.Unicode, rect));
                    }

                    advance += glyph.Advance;
                }
            }

            i = j;
        }

        float fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        return new SKSize(TextRenderUtilities.GetTextWidth(glyphs) * fullHorizontalScale, 0);
    }
}
