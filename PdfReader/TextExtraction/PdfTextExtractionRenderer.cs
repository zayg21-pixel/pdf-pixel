using PdfReader.Color.Paint;
using PdfReader.Fonts.Model;
using PdfReader.Forms;
using PdfReader.Imaging.Model;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Rendering.State;
using PdfReader.Rendering.Text;
using PdfReader.Shading.Model;
using PdfReader.Text;
using SkiaSharp;
using System.Collections.Generic;

namespace PdfReader.TextExtraction;

// TODO: [MEDIUM] refactor
internal class PdfTextExtractionRenderer : IPdfRenderer
{
    public List<PdfCharacter> PageCharacters { get; } = new List<PdfCharacter>();

    public void DrawForm(SKCanvas canvas, PdfForm formXObject, PdfGraphicsState graphicsState)
    {
        canvas.Save();

        // Apply form matrix if present
        canvas.Concat(formXObject.Matrix);
        canvas.ClipRect(formXObject.BBox);

        // Decode and render content with a cloned state that clears parent soft mask
        var content = formXObject.GetFormData();
        if (!content.IsEmpty)
        {
            var parseContext = new PdfParseContext(content);
            var formPage = formXObject.GetFormPage();
            var localGs = graphicsState.Clone();
            localGs.CTM = formXObject.Matrix;

            var renderer = new PdfContentStreamRenderer(this, formPage);
            renderer.RenderContext(canvas, ref parseContext, localGs);
        }

        canvas.Restore();
    }

    public void DrawImage(SKCanvas canvas, PdfImage pdfImage, PdfGraphicsState state)
    {
        // no op
    }

    public void DrawPath(SKCanvas canvas, SKPath path, PdfGraphicsState state, PaintOperation operation, SKPathFillType fillType)
    {
        // no op
    }

    public void DrawShading(SKCanvas canvas, PdfShading shading, PdfGraphicsState state)
    {
        // no op
    }

    public SKSize DrawTextSequence(SKCanvas canvas, List<ShapedGlyph> glyphs, PdfGraphicsState state, PdfFontBase font)
    {
        throw new System.NotImplementedException();

        //var currentMatrix = SKMatrix.Concat(canvas.TotalMatrix, TextRenderUtilities.GetFullTextMatrix(0, state));
        //var rawTextMatrix = SKMatrix.Concat(canvas.TotalMatrix, state.TextMatrix);

        //float advance = 0;

        //// Use font metrics for correct vertical bounds
        //var metrics = skFont.Metrics;
        //float top = metrics.Ascent;
        //float bottom = metrics.Descent;

        //foreach (var glyph in glyphs)
        //{
        //    // Use font metrics for vertical bounds
        //    var rect = new SKRect(advance, top, advance + glyph.Width, bottom);
        //    rect = currentMatrix.MapRect(rect).Standardized;

        //    if (rect.Width != 0)
        //    {
        //        PageCharacters.Add(new PdfCharacter(glyph.Unicode, rect));
        //    }

        //    advance += glyph.TotalWidth;
        //}

        //return advance * state.FontSize * state.HorizontalScaling / 100f;
    }
}
