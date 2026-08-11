using Microsoft.Extensions.Logging;
using PdfPixel.Commands;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Forms;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.State;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Model;

/// <summary>
/// Type 3 fonts (user-defined fonts)
/// Contains custom glyph definitions in PDF content streams
/// Each character is defined by a PDF content stream that draws the glyph
/// Limited to 256 characters (single-byte encoding)
/// </summary>
public class PdfType3Font : PdfSingleByteFont
{
    private readonly Dictionary<PdfCharacterCode, PdfType3CharacterInfo> type3Cache = [];

    /// <summary>
    /// Constructor for Type3 fonts - lightweight operations only
    /// </summary>
    /// <param name="fontObject">PDF object containing the font definition</param>
    internal PdfType3Font(PdfObject fontObject)
        : base(fontObject)
    {
        if (Type != PdfFontSubType.Type3)
        {
            throw new ArgumentException("Font dictionary must be Type3");
        }

        // Get CharProcs dictionary - essential for Type3 fonts
        CharProcs = Dictionary.GetDictionary(PdfTokens.CharProcsKey);

        // Get FontMatrix (required for Type3 fonts)
        FontMatrix = PdfMatrix.FromArray(Dictionary.GetArray(PdfTokens.FontMatrixKey)) ?? PdfMatrix.CreateScale(0.001f, 0.001f);

        // Get FontBBox (required for Type3 fonts per PDF spec §9.6.5, Table 110)
        // Used as the recording bounds for d0 glyphs that do not specify a per-glyph bounding box
        FontBBox = PdfRectangle.FromArray(Dictionary.GetArray(PdfTokens.FontBBoxKey));
        Widths.RescaleWidths(FontMatrix.ScaleX / PdfSingleByteFontWidths.WidthToUserSpaceCoeff);

        if (Encoding.BaseEncoding == PdfEncoding.Unknown)
        {
            // Default to StandardEncoding for Type3 fonts if no encoding specified
            Encoding.UpdateEncoding(PdfEncoding.StandardEncoding);
        }
    }

    /// <summary>
    /// Type 3 fonts do not use an embedded typeface; always returns <see langword="null"/>.
    /// Glyphs are rendered from PDF content streams rather than a conventional typeface.
    /// </summary>
    protected internal override IPdfTypeface? Typeface => null;

    /// <summary>
    /// Type 3 glyphs are never shaped against a typeface, substitute or otherwise; always returns
    /// <see langword="false"/>.
    /// </summary>
    protected internal override bool IsSubstitutedFont => false;

    /// <summary>
    /// Character procedures dictionary containing glyph definitions
    /// Each entry maps a character name to a content stream that draws the glyph
    /// Set during construction - lightweight operation
    /// </summary>
    public PdfDictionary? CharProcs { get; }

    /// <summary>
    /// Font transformation matrix (required for Type3 fonts)
    /// Maps from glyph space to text space
    /// </summary>
    public PdfMatrix FontMatrix { get; }

    /// <summary>
    /// Font bounding box in glyph coordinate space (required for Type3 fonts per PDF spec §9.6.5, Table 110).
    /// Used as the recording bounds for d0 glyphs that do not define a per-glyph bounding box via d1.
    /// </summary>
    public PdfRectangle? FontBBox { get; }

    /// <summary>
    /// Renders a Type 3 character CharProc to a recorded picture and extracts d0/d1 metrics from the glyph graphics state.
    /// Results are cached per character code for reuse.
    /// </summary>
    public PdfType3CharacterInfo GetCharacterInfo(PdfCharacterCode charCode, IPdfRenderer renderer, PdfGraphicsState sourceState)
    {
        if (charCode == null)
        {
            throw new ArgumentNullException(nameof(charCode));
        }

        if (renderer == null)
        {
            throw new ArgumentNullException(nameof(renderer));
        }

        if (sourceState == null)
        {
            throw new ArgumentNullException(nameof(sourceState));
        }

        if (CharProcs == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        if (type3Cache.TryGetValue(charCode, out PdfType3CharacterInfo? cached))
        {
            return cached;
        }

        // Convert character code to character name
        PdfString charName = GetCharacterName(charCode);
        if (charName.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        PdfObject? charObject = CharProcs.GetObject(charName);
        if (charObject == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        ReadOnlyMemory<byte> streamData = charObject.DecodeAsMemory();

        if (streamData.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        if (sourceState.RecursionGuard.Contains(charObject.Reference.ObjectNumber))
        {
            return PdfType3CharacterInfo.Undefined;
        }

        sourceState.RecursionGuard.Add(charObject.Reference.ObjectNumber);

        PdfCommandRecorder recorder = new();

        // Render glyph content stream without recursion (independent from page rendering)
        FormXObjectPageWrapper glyphPage = new(sourceState.Page, FontObject);
        PdfContentStreamRenderer contentRenderer = new(renderer, glyphPage);
        PdfParseContext parseContext = new(streamData);

        (PdfSize advancement, PdfRectangle? boundingBox) = ParseMetrics(parseContext);

        PdfGraphicsState charState = new(glyphPage, sourceState.RecursionGuard, default, sourceState.RenderingParameters);

        PdfRectangle? glyphBounds = boundingBox ?? FontBBox;
        if (glyphBounds != null)
        {
            charState.ClipBounds = charState.CTM.MapRect(glyphBounds.Value);
        }

        contentRenderer.RenderContext(recorder, ref parseContext, charState);

        PdfType3CharacterInfo info = new(recorder, boundingBox, advancement);
        type3Cache[charCode] = info;

        sourceState.RecursionGuard.Remove(charObject.Reference.ObjectNumber);

        return info;
    }

    private (PdfSize advancement, PdfRectangle? boundingBox) ParseMetrics(PdfParseContext parseContext)
    {
        PdfParser parser = new(parseContext, Document, allowReferences: false, decrypt: false);
        IPdfValue? value;
        Stack<IPdfValue> operandStack = [];

        PdfSize type3Advancement = new(0, 0);
        PdfRectangle? type3BoundingBox = null;
        while ((value = parser.ReadNextValue()) != null)
        {
            if (value.Type == PdfValueType.Operator)
            {
                string op = value.AsString().ToString();

                switch (op)
                {
                    case "d0":
                    {
                            List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(6, operandStack);

                        if (operands.Count < 2)
                        {
                            break;
                        }

                        float wx = operands[0].AsFloat();
                        float wy = operands[1].AsFloat();
                        type3Advancement = new PdfSize(wx, wy);
                        return (type3Advancement, type3BoundingBox);
                    }
                    case "d1":
                    {
                            List<IPdfValue> operands = PdfOperatorProcessor.GetOperands(6, operandStack);

                        if (operands.Count < 6)
                        {
                            break;
                        }

                        float wx = operands[0].AsFloat();
                        float wy = operands[1].AsFloat();
                        float llx = operands[2].AsFloat();
                        float lly = operands[3].AsFloat();
                        float urx = operands[4].AsFloat();
                        float ury = operands[5].AsFloat();

                        type3Advancement = new PdfSize(wx, wy);
                        type3BoundingBox = new PdfRectangle(Math.Min(llx, urx), Math.Min(lly, ury), Math.Max(llx, urx), Math.Max(lly, ury));

                        return (type3Advancement, type3BoundingBox);
                    }
                }
            }
            else
            {
                operandStack.Push(value);
            }
        }

        return (type3Advancement, type3BoundingBox);
    }

    /// <summary>
    /// Convert character code to character name based on encoding
    /// </summary>
    private PdfString GetCharacterName(PdfCharacterCode charCode) => Encoding.GetNameByCode((byte)charCode).ToPdfString();

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code in a Type3 font.
    /// Type3 fonts do not use GIDs; always returns 1.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>Always 1 for Type3 fonts.</returns>
    public override ushort? GetGid(PdfCharacterCode code) => 1;

    /// <summary>
    /// A Type3 glyph is drawn by running the character procedure its /CharProcs names for the code, so
    /// no typeface supplies its outline. A typeface is still resolved to carry the character's metrics,
    /// which come from this font's own /Widths.
    /// </summary>
    protected override PdfGlyphResolution ResolveGlyphs(PdfCharacterCode characterCode, string? renderingUnicode)
    {
        PdfGlyphResolution resolution = SubstituteByUnicode(renderingUnicode);
        IPdfTypeface typeface = resolution.Typeface ?? Document.FontProvider.GetFallbackTypeface(SubstitutionInfo);

        return new PdfGlyphResolution(typeface, [GetGid(characterCode)], isMappedByFont: true);
    }
}
