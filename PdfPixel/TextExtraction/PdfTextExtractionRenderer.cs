using PdfPixel.Color.Paint;
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
    public List<PdfCharacter> PageCharacters { get; } = new List<PdfCharacter>();

    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        int count = canvas.Save();

        // Apply form matrix
        canvas.Concat(formXObject.Matrix);

        canvas.ClipRect(formXObject.BBox);

        // Decode and render content with a cloned state
        var content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            var parseContext = new PdfParseContext(content);
            var formPage = formXObject.GetFormPage();
            var localGs = new PdfGraphicsState(formPage, graphicsState);
            localGs.CTM = formXObject.Matrix;

            var renderer = new PdfContentStreamRenderer(this, formPage);
            renderer.RenderContext(canvas, ref parseContext, localGs);
        }

        canvas.RestoreToCount(count);
    }

    public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state)
    {
        // no op
    }

    public void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation)
    {
        // no op
    }

    public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state)
    {
        // no op
    }

    public SKSize DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        var currentMatrix = SKMatrix.Concat(canvas.TotalMatrix, TextRenderUtilities.GetFullTextMatrix(state));
        float advance = 0;
        int i = 0;
        while (i < glyphs.Count)
        {
            var groupTypeface = glyphs[i].CharacterInfo.Typeface;
            var groupScale = glyphs[i].Scale;
            int j = i;
            // Find run of glyphs with same typeface and scale
            while (j < glyphs.Count && glyphs[j].CharacterInfo.Typeface == groupTypeface && glyphs[j].Scale == groupScale)
            {
                j++;
            }

            using (var skFont = PdfPaintFactory.CreateTextFont(groupTypeface))
            {
                skFont.ScaleX = groupScale;
                var metrics = skFont.Metrics;
                float top = metrics.Ascent;
                float bottom = metrics.Descent;

                for (int k = i; k < j; k++)
                {
                    var glyph = glyphs[k];
                    var rect = new SKRect(advance, top, advance + glyph.CharacterInfo.Advancement, bottom);
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

        var fullHorizontalScale = state.FontSize * state.HorizontalScaling / 100f;
        return new SKSize(TextRenderUtilities.GetTextWidth(glyphs) * fullHorizontalScale, 0);
    }
}
