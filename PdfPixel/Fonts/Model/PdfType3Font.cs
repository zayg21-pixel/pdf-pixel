using Microsoft.Extensions.Logging;
using PdfPixel.Commands.Model;
using PdfPixel.Fonts.Mapping;
using PdfPixel.Geometry;
using PdfPixel.Models;
using PdfPixel.Parsing;
using PdfPixel.Rendering;
using PdfPixel.Rendering.Operators;
using PdfPixel.Rendering.State;
using PdfPixel.Tagging.Model;
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
    private readonly Dictionary<PdfCharacterCode, PdfType3CharacterInfo> _characterInfos = [];
    private readonly PdfResourceNode? _resources;

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

        CharProcs = Dictionary.GetDictionary(PdfTokens.CharProcsKey);

        PdfResourceDictionary? resources = PdfResourceDictionary.GetResourceDictionary(Dictionary, PdfTokens.ResourcesKey);
        if (resources != null)
        {
            _resources = new PdfResourceNode(Document, resources, null);
        }

        FontMatrix = PdfMatrix.FromArray(Dictionary.GetArray(PdfTokens.FontMatrixKey)) ?? PdfMatrix.CreateScale(0.001f, 0.001f);

        FontBoundingBox = PdfRectangle.FromArray(Dictionary.GetArray(PdfTokens.FontBBoxKey));
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
    public PdfRectangle? FontBoundingBox { get; }

    /// <summary>
    /// Gets the character procedure and the d0/d1 metrics of a Type 3 character. They do not depend on the
    /// graphics state, so they are read once per character code.
    /// </summary>
    public PdfType3CharacterInfo GetCharacterInfo(in PdfCharacterCode charCode)
    {
        if (_characterInfos.TryGetValue(charCode, out PdfType3CharacterInfo? cached))
        {
            return cached;
        }

        PdfType3CharacterInfo info = ReadCharacterInfo(charCode);
        _characterInfos[charCode] = info;

        return info;
    }

    /// <summary>
    /// Records the glyph description of a Type 3 character shown in <paramref name="sourceState"/>. The glyph inherits
    /// the graphics state of the text-showing operator aside from the CTM, so it is recorded for each use.
    /// </summary>
    /// <param name="info">The character, from <see cref="GetCharacterInfo"/>.</param>
    /// <param name="renderer">Renderer executing the glyph description.</param>
    /// <param name="sourceState">Graphics state of the text-showing operator.</param>
    /// <returns>The glyph recorded in glyph space, or null when the character is undefined or recursive.</returns>
    internal PdfCommandRecorder? RecordCharacter(PdfType3CharacterInfo info, IPdfRenderer renderer, PdfGraphicsState sourceState)
    {
        if (!info.IsDefined || sourceState.RecursionGuard.Contains(info.ObjectNumber))
        {
            return null;
        }

        sourceState.RecursionGuard.Add(info.ObjectNumber);

        IPdfContentHost sourceHost = sourceState.Host;
        PdfContentHost glyphHost = new(sourceHost.Document, _resources ?? sourceHost.Resources, null);
        PdfContentStreamRenderer contentRenderer = new(renderer, glyphHost);
        PdfParseContext parseContext = new(info.Content, PdfParseContext.ContentStreamValuesCapacity);
        PdfGraphicsState charState = sourceState.CloneForType3Glyph(glyphHost);

        // TODO: [MEDIUM] Type3 text not selectable (issue19624.pdf): d0 glyphs without d1 bbox and FontBBox [0 0 0 0]
        // get zero-size character bounds. Treat an all-zero FontBBox as absent (spec: no assumptions), add
        // IContentRenderCommand with Bounds on every Draw*Command (render targets already compute them, outer space,
        // independent of the command's own matrix), a general bounds walker over recordings, then take glyph bounds
        // live from the recording when d1 bbox is absent.
        PdfRectangle? glyphBounds = info.BoundingBox ?? FontBoundingBox;
        if (glyphBounds != null)
        {
            charState.ClipBounds = glyphBounds.Value;
        }

        PdfCommandRecorder recorder = new();
        PdfContentStreamScope contentStreamScope = new(PdfContentStreamType.Type3Glyph);
        recorder.Process(new BeginScopeCommand(contentStreamScope));
        contentRenderer.RenderContext(recorder, ref parseContext, charState);
        recorder.Process(new EndScopeCommand());

        sourceState.RecursionGuard.Remove(info.ObjectNumber);

        return recorder;
    }

    private PdfType3CharacterInfo ReadCharacterInfo(in PdfCharacterCode charCode)
    {
        if (CharProcs == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        PdfString charName = GetCharacterName(charCode);
        if (charName.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        PdfObject? charObject = CharProcs.GetObject(charName);
        if (charObject == null || charObject.Reference == null)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        ReadOnlyMemory<byte> streamData = charObject.Stream.DecodeAsMemory();
        if (streamData.IsEmpty)
        {
            return PdfType3CharacterInfo.Undefined;
        }

        (PdfSize advancement, PdfRectangle? boundingBox) = ParseMetrics(new PdfParseContext(streamData, PdfParseContext.ContentStreamValuesCapacity));

        return new PdfType3CharacterInfo(streamData, charObject.Reference.Value.ObjectNumber, boundingBox, advancement);
    }

    private (PdfSize advancement, PdfRectangle? boundingBox) ParseMetrics(PdfParseContext parseContext)
    {
        PdfParser parser = new(parseContext, Document, PdfParserMode.ContentStream);
        PdfOperandStack operandStack = new();

        PdfSize type3Advancement = new(0, 0);
        PdfRectangle? type3BoundingBox = null;
        while (parser.TryReadContentStreamValue(out PdfContentStreamValue value))
        {
            if (value.Kind == PdfContentStreamValueKind.Operator)
            {
                switch (value.GetOperator())
                {
                    case PdfOperator.GlyphWidth:
                    {
                            ReadOnlySpan<PdfContentStreamValue> operands = operandStack.Pop(6);

                        if (operands.Length < 2)
                        {
                            break;
                        }

                        float? wx = operands[0].AsFloat();
                        float? wy = operands[1].AsFloat();

                        if (wx == null || wy == null)
                        {
                            break;
                        }

                        type3Advancement = new PdfSize(wx.Value, wy.Value);
                        return (type3Advancement, type3BoundingBox);
                    }
                    case PdfOperator.GlyphWidthAndBoundingBox:
                    {
                            ReadOnlySpan<PdfContentStreamValue> operands = operandStack.Pop(6);

                        if (operands.Length < 6)
                        {
                            break;
                        }

                        float? wx = operands[0].AsFloat();
                        float? wy = operands[1].AsFloat();
                        float? llx = operands[2].AsFloat();
                        float? lly = operands[3].AsFloat();
                        float? urx = operands[4].AsFloat();
                        float? ury = operands[5].AsFloat();

                        if (wx == null
                            || wy == null
                            || llx == null
                            || lly == null
                            || urx == null
                            || ury == null)
                        {
                            break;
                        }

                        type3Advancement = new PdfSize(wx.Value, wy.Value);
                        type3BoundingBox = new PdfRectangle(Math.Min(llx.Value, urx.Value), Math.Min(lly.Value, ury.Value), Math.Max(llx.Value, urx.Value), Math.Max(lly.Value, ury.Value));

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
    /// Converts a character code to a character name based on the encoding.
    /// </summary>
    private PdfString GetCharacterName(in PdfCharacterCode charCode) => Encoding.GetNameByCode((byte)charCode.Value).ToPdfString();

    /// <summary>
    /// Gets the advance width for the specified character code in a Type3 font.
    /// A code the font's /Widths array does not cover has a width of zero.
    /// </summary>
    /// <param name="code">The character code to retrieve the width for.</param>
    /// <returns>The advance width in user space units.</returns>
    public override float? GetWidth(in PdfCharacterCode code) => base.GetWidth(code) ?? 0f;

    /// <summary>
    /// Gets the glyph ID (GID) for the specified character code in a Type3 font.
    /// Type3 fonts do not use GIDs; always returns <see langword="null"/>.
    /// </summary>
    /// <param name="code">The character code to map to a glyph ID.</param>
    /// <returns>Always <see langword="null"/> for Type3 fonts.</returns>
    public override ushort? GetGid(in PdfCharacterCode code) => null;

    /// <summary>
    /// A Type3 glyph is drawn by running the character procedure its /CharProcs names for the code, so
    /// no typeface supplies its outline. A typeface is still resolved to carry the character's metrics,
    /// which come from this font's own /Widths.
    /// </summary>
    protected override PdfGlyphResolution ResolveGlyphs(in PdfCharacterCode characterCode, string? renderingUnicode)
    {
        PdfGlyphResolution resolution = SubstituteByUnicode(renderingUnicode);
        IPdfTypeface typeface = resolution.Typeface ?? Document.FontProvider.GetFallbackTypeface(SubstitutionInfo);

        return new PdfGlyphResolution(typeface, [GetGid(characterCode)], isMappedByFont: true);
    }
}
