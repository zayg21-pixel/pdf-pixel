using PdfReader.Fonts.Mapping;
using PdfReader.Forms;
using PdfReader.Models;
using PdfReader.Parsing;
using PdfReader.Rendering;
using PdfReader.Rendering.Operators;
using PdfReader.Rendering.State;
using PdfReader.Text;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PdfReader.Fonts.Model;

/// <summary>
/// Type 3 fonts (user-defined fonts)
/// Contains custom glyph definitions in PDF content streams
/// Each character is defined by a PDF content stream that draws the glyph
/// Limited to 256 characters (single-byte encoding)
/// </summary>
public class PdfType3Font : PdfSingleByteFont
{
    private readonly Dictionary<PdfCharacterCode, PdfType3CharacterInfo> type3Cache = new Dictionary<PdfCharacterCode, PdfType3CharacterInfo>();

    /// <summary>
    /// Constructor for Type3 fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    public PdfType3Font(PdfObject fontObject) : base(fontObject)
    {
        if (Type != PdfFontSubType.Type3)
        {
            throw new ArgumentException("Font dictionary must be Type3");
        }

        // Get CharProcs dictionary - essential for Type3 fonts
        CharProcs = Dictionary.GetDictionary(PdfTokens.CharProcsKey);

        // Get FontMatrix (required for Type3 fonts)
        FontMatrix = PdfLocationUtilities.CreateMatrix(Dictionary.GetArray(PdfTokens.FontMatrixKey)) ?? SKMatrix.CreateScale(0.001f, 0.001f);

        // Rescale width to glyph space
        Widths.RescaleWidths(FontMatrix.ScaleX / SingleByteFontWidths.WidthToUserSpaceCoeff);

        if (Encoding.BaseEncoding == PdfFontEncoding.Unknown)
        {
            // Default to StandardEncoding for Type3 fonts if no encoding specified
            Encoding.Update(PdfFontEncoding.StandardEncoding, default);
        }
    }

    internal protected override SKTypeface Typeface => null;
    
    /// <summary>
    /// Character procedures dictionary containing glyph definitions
    /// Each entry maps a character name to a content stream that draws the glyph
    /// Set during construction - lightweight operation
    /// </summary>
    public PdfDictionary CharProcs { get; }

    /// <summary>
    /// Font transformation matrix (required for Type3 fonts)
    /// Maps from glyph space to text space
    /// </summary>
    public SKMatrix FontMatrix { get; }

    /// <summary>
    /// Type3 fonts don't have embedded font files, but their glyphs are defined within the PDF itself.
    /// </summary>
    public override bool IsEmbedded => true;

    /// <summary>
    /// Renders a Type 3 character CharProc to a recorded picture and extracts d0/d1 metrics from the glyph graphics state.
    /// Results are cached per character code for reuse.
    /// </summary>
    public PdfType3CharacterInfo GetCharacterInfo(PdfCharacterCode charCode, IPdfRenderer renderer, PdfGraphicsState sourceState)
    {
        if (CharProcs == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        if (type3Cache.TryGetValue(charCode, out var cached))
        {
            return cached;
        }

        // Convert character code to character name
        var charName = GetCharacterName(charCode);
        if (charName.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        var charObject = CharProcs.GetObject(charName);
        if (charObject == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        var streamData = charObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        if (sourceState.RecursionGuard.Contains(charObject.Reference.ObjectNumber))
        {
            return PdfType3CharacterInfo.Undefined;
        }

        sourceState.RecursionGuard.Add(charObject.Reference.ObjectNumber);

        float width = Widths.GetWidth(charCode) ?? 1f;
        float height = 1f;
        var charState = new PdfGraphicsState(sourceState);

        var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(FontMatrix.Invert().MapRect(new SKRect(0, 0, width, height)));

        // Apply Type3 font matrix to draw glyph stream in glyph space
        canvas.Concat(FontMatrix);

        // Render glyph content stream without recursion (independent from page rendering)
        var glyphPage = new FormXObjectPageWrapper(sourceState.Page, FontObject);
        var contentRenderer = new PdfContentStreamRenderer(renderer, glyphPage);
        var parseContext = new PdfParseContext(streamData);

        contentRenderer.RenderContext(canvas, ref parseContext, charState);

        var picture = recorder.EndRecording();

        // Extract d0/d1 metrics captured into charState by MiscellaneousOperators (d0/d1)
        SKRect? bbox = charState.Type3BoundingBox;
        SKSize adv = charState.Type3Advancement; // default SKSize.Empty when not set

        var info = new PdfType3CharacterInfo(picture, bbox, adv);
        type3Cache[charCode] = info;

        sourceState.RecursionGuard.Remove(charObject.Reference.ObjectNumber);

        return info;
    }

    /// <summary>
    /// Convert character code to character name based on encoding
    /// </summary>
    private PdfString GetCharacterName(PdfCharacterCode charCode)
    {
        return SingleByteEncodings.GetNameByCode((byte)charCode, Encoding.BaseEncoding, Encoding.Differences);
    }

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code in a Type3 font.
    /// Type3 fonts do not use GIDs; always returns 0.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>Always 1 for Type3 fonts.</returns>
    public override ushort GetGid(PdfCharacterCode code)
    {
        return 1;
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            foreach (var info in type3Cache.Values)
            {
                info.Picture?.Dispose();
            }

            type3Cache.Clear();
        }
    }
}