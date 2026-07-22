using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Evaluates a Type2 charstring into a <see cref="CffCharacter"/>: inlines <c>callsubr</c>/<c>callgsubr</c>
/// (bias-adjusted, from local and global Subr INDEXes), drops hint operators (<c>hstem</c>/<c>vstem</c>/
/// <c>hstemhm</c>/<c>vstemhm</c>/<c>hintmask</c>/<c>cntrmask</c> -- tracking hint count only to skip
/// <c>hintmask</c>/<c>cntrmask</c> mask bytes correctly), builds the outline via
/// <see cref="PdfPixel.Fonts.Model.PdfFontPathBuilder"/>, and emits a flattened, hint-free,
/// subroutine-free repacked charstring alongside the extracted width.
/// </summary>
public class CffCharStringEvaluator
{
    private static readonly PdfFontString[] StandardEncoding = SingleByteEncodings.GetEncodingSet(PdfFontEncoding.StandardEncoding) ?? Array.Empty<PdfFontString>();

    private readonly ILogger<CffCharStringEvaluator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffCharStringEvaluator"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during evaluation.</param>
    public CffCharStringEvaluator(ILogger<CffCharStringEvaluator> logger) => _logger = logger;

    /// <summary>
    /// Evaluates a single charstring.
    /// </summary>
    /// <param name="charString">The raw charstring bytes.</param>
    /// <param name="topDict">The font's own Top DICT and (for a non-CID font) Private DICT. Used for
    /// <c>nominalWidthX</c>/<c>defaultWidthX</c> when <paramref name="dict"/> is null or has no Private
    /// DICT of its own.</param>
    /// <param name="dict">For a CID-keyed font, the FDArray entry this specific character's FDSelect
    /// entry names; null for a non-CID font, which has no per-character Font DICT. When non-null, its
    /// Private DICT takes precedence over <paramref name="topDict"/>'s for
    /// <c>nominalWidthX</c>/<c>defaultWidthX</c>. Stored on the returned <see cref="CffCharacter"/> as-is.</param>
    /// <param name="localSubrs">The Local Subr INDEX entries in scope for this charstring.</param>
    /// <param name="globalSubrs">The typeface's Global Subr INDEX entries.</param>
    /// <param name="charStrings">This font's raw CharStrings INDEX entries, indexed by GID -- used to
    /// resolve the base/accent glyphs of a deprecated seac-style <c>endchar</c> accent composition.</param>
    /// <param name="nameToGid">This font's glyph-name-to-GID map, used for the same purpose. Empty for
    /// CID-keyed fonts.</param>
    /// <returns>The evaluated character.</returns>
    public CffCharacter Evaluate(
        in ReadOnlyMemory<byte> charString,
        CffFontDict topDict,
        CffFontDict? dict,
        ReadOnlyMemory<byte>[] localSubrs,
        ReadOnlyMemory<byte>[] globalSubrs,
        ReadOnlyMemory<byte>[] charStrings,
        IReadOnlyDictionary<PdfFontString, ushort> nameToGid)
    {
        if (topDict == null)
        {
            throw new ArgumentNullException(nameof(topDict));
        }

        if (localSubrs == null)
        {
            throw new ArgumentNullException(nameof(localSubrs));
        }

        if (globalSubrs == null)
        {
            throw new ArgumentNullException(nameof(globalSubrs));
        }

        if (charStrings == null)
        {
            throw new ArgumentNullException(nameof(charStrings));
        }

        if (nameToGid == null)
        {
            throw new ArgumentNullException(nameof(nameToGid));
        }

        CffPrivateDict? effectivePrivateDict = dict?.PrivateDict ?? topDict.PrivateDict;
        float nominalWidthX = effectivePrivateDict?.NominalWidthX ?? 0f;
        float defaultWidthX = effectivePrivateDict?.DefaultWidthX ?? 0f;
        PdfFontMatrix matrix = CffTopDict.CombineFontMatrix(topDict.TopDict, dict?.TopDict);
        CffCharStringContext context = new(localSubrs, globalSubrs, nominalWidthX, defaultWidthX, charStrings, nameToGid, matrix);

        Execute(charString.Span, context, depth: 0);

        if (context.SawMoveTo)
        {
            context.PathBuilder.Close();
        }

        if (context.Width != context.DefaultWidthX)
        {
            context.Writer.PrependWidth(context.Width - context.NominalWidthX);
        }

        return new CffCharacter(context.PathBuilder.ToPath(), context.Writer.ToArray(), context.Width, dict);
    }

    private void Execute(in ReadOnlySpan<byte> data, CffCharStringContext context, int depth)
    {
        if (depth > CffConstants.MaxSubroutineNestingDepth)
        {
            _logger.LogWarning("CFF charstring exceeded max subroutine nesting depth ({MaxDepth}); aborting this branch.", CffConstants.MaxSubroutineNestingDepth);
            return;
        }

        CffCharStringReader reader = new(data);
        ICffValue? value = reader.ReadNextValue();
        while (!context.Finished && value != null)
        {
            switch (value.Type)
            {
                case CffValueType.Integer:
                case CffValueType.Real:
                    {
                        context.Operands.Add(value.AsFloat());
                        break;
                    }
                case CffValueType.Operator:
                    {
                        byte operatorCode = ((CffValue<byte>)value).Value;
                        if (!ApplyOperator(ref reader, context, operatorCode, depth))
                        {
                            return;
                        }

                        break;
                    }
                case CffValueType.EscapedOperator:
                    {
                        byte operatorCode = ((CffValue<byte>)value).Value;
                        ApplyEscapedOperator(context, operatorCode);
                        break;
                    }
            }

            value = reader.ReadNextValue();
        }
    }

    // Returns false when this frame must stop processing further bytes (return/endchar/error).
    private bool ApplyOperator(ref CffCharStringReader reader, CffCharStringContext context, byte operatorCode, int depth)
    {
        switch (operatorCode)
        {
            case CffConstants.CharStringOperatorHstem:
            case CffConstants.CharStringOperatorVstem:
            case CffConstants.CharStringOperatorHstemhm:
            case CffConstants.CharStringOperatorVstemhm:
                {
                    List<float> args = PopAllWidth(context, nominalCountIsOdd: false);
                    context.HintCount += args.Count / 2;
                    break;
                }
            case CffConstants.CharStringOperatorHintmask:
            case CffConstants.CharStringOperatorCntrmask:
                {
                    if (context.HintMaskBytes == 0)
                    {
                        List<float> args = PopAllWidth(context, nominalCountIsOdd: false);
                        context.HintCount += args.Count / 2;
                        context.HintMaskBytes = (context.HintCount + 7) / 8;
                    }
                    else
                    {
                        context.Operands.Clear();
                    }

                    if (!reader.TrySkipBytes(context.HintMaskBytes))
                    {
                        _logger.LogWarning("CFF charstring hintmask/cntrmask ran past the end of the data.");
                        return false;
                    }

                    break;
                }
            case CffConstants.CharStringOperatorRmoveto:
                {
                    List<float> args = PopAllWidth(context, nominalCountIsOdd: false);
                    float dx = (args.Count > 0) ? args[0] : 0f;
                    float dy = (args.Count > 1) ? args[1] : 0f;
                    EmitMoveTo(context, dx, dy);
                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRmoveto);
                    break;
                }
            case CffConstants.CharStringOperatorHmoveto:
                {
                    List<float> args = PopAllWidth(context, nominalCountIsOdd: true);
                    float dx = (args.Count > 0) ? args[0] : 0f;
                    EmitMoveTo(context, dx, 0f);
                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorHmoveto);
                    break;
                }
            case CffConstants.CharStringOperatorVmoveto:
                {
                    List<float> args = PopAllWidth(context, nominalCountIsOdd: true);
                    float dy = (args.Count > 0) ? args[0] : 0f;
                    EmitMoveTo(context, 0f, dy);
                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorVmoveto);
                    break;
                }
            case CffConstants.CharStringOperatorRlineto:
                {
                    List<float> args = context.PopAll();
                    for (int index = 0; index + 1 < args.Count; index += 2)
                    {
                        EmitLine(context, args[index], args[index + 1]);
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRlineto);
                    break;
                }
            case CffConstants.CharStringOperatorHlineto:
            case CffConstants.CharStringOperatorVlineto:
                {
                    List<float> args = context.PopAll();
                    bool horizontal = operatorCode == CffConstants.CharStringOperatorHlineto;
                    for (int index = 0; index < args.Count; index++)
                    {
                        if (horizontal)
                        {
                            EmitLine(context, args[index], 0f);
                        }
                        else
                        {
                            EmitLine(context, 0f, args[index]);
                        }

                        horizontal = !horizontal;
                    }

                    context.Writer.WriteOperator(args, operatorCode);
                    break;
                }
            case CffConstants.CharStringOperatorRrcurveto:
                {
                    List<float> args = context.PopAll();
                    for (int index = 0; index + 5 < args.Count; index += 6)
                    {
                        EmitCurve(context, args[index], args[index + 1], args[index + 2], args[index + 3], args[index + 4], args[index + 5]);
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRrcurveto);
                    break;
                }
            case CffConstants.CharStringOperatorRcurveline:
                {
                    List<float> args = context.PopAll();
                    int index = 0;
                    for (; index + 7 < args.Count; index += 6)
                    {
                        EmitCurve(context, args[index], args[index + 1], args[index + 2], args[index + 3], args[index + 4], args[index + 5]);
                    }

                    if (index + 1 < args.Count)
                    {
                        EmitLine(context, args[index], args[index + 1]);
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRcurveline);
                    break;
                }
            case CffConstants.CharStringOperatorRlinecurve:
                {
                    List<float> args = context.PopAll();
                    int lineCount = args.Count - 6;
                    int index = 0;
                    for (; index < lineCount; index += 2)
                    {
                        EmitLine(context, args[index], args[index + 1]);
                    }

                    if (index + 5 < args.Count)
                    {
                        EmitCurve(context, args[index], args[index + 1], args[index + 2], args[index + 3], args[index + 4], args[index + 5]);
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRlinecurve);
                    break;
                }
            case CffConstants.CharStringOperatorVvcurveto:
                {
                    List<float> args = context.PopAll();
                    int index = 0;
                    float dx1 = 0f;
                    if ((args.Count % 2) != 0)
                    {
                        dx1 = args[0];
                        index = 1;
                    }

                    for (; index + 3 < args.Count; index += 4)
                    {
                        EmitCurve(context, dx1, args[index], args[index + 1], args[index + 2], 0f, args[index + 3]);
                        dx1 = 0f;
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorVvcurveto);
                    break;
                }
            case CffConstants.CharStringOperatorHhcurveto:
                {
                    List<float> args = context.PopAll();
                    int index = 0;
                    float dy1 = 0f;
                    if ((args.Count % 2) != 0)
                    {
                        dy1 = args[0];
                        index = 1;
                    }

                    for (; index + 3 < args.Count; index += 4)
                    {
                        EmitCurve(context, args[index], dy1, args[index + 1], args[index + 2], args[index + 3], 0f);
                        dy1 = 0f;
                    }

                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorHhcurveto);
                    break;
                }
            case CffConstants.CharStringOperatorVhcurveto:
                {
                    List<float> args = context.PopAll();
                    EmitAlternatingCurves(context, args, startVertical: true);
                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorVhcurveto);
                    break;
                }
            case CffConstants.CharStringOperatorHvcurveto:
                {
                    List<float> args = context.PopAll();
                    EmitAlternatingCurves(context, args, startVertical: false);
                    context.Writer.WriteOperator(args, CffConstants.CharStringOperatorHvcurveto);
                    break;
                }
            case CffConstants.CharStringOperatorCallsubr:
                {
                    CallSubroutine(context, context.LocalSubrs, context.LocalBias, depth);
                    break;
                }
            case CffConstants.CharStringOperatorCallgsubr:
                {
                    CallSubroutine(context, context.GlobalSubrs, context.GlobalBias, depth);
                    break;
                }
            case CffConstants.CharStringOperatorReturn:
                {
                    return false;
                }
            case CffConstants.CharStringOperatorEndchar:
                {
                    List<float> args = PopAllWidth(context, nominalCountIsOdd: false);

                    // A component's own endchar (reached while composing a seac accent below) only ends
                    // that component's charstring, not the overall composite glyph.
                    if (context.InSeacComponent)
                    {
                        return false;
                    }

                    if (args.Count == 4)
                    {
                        ApplySeacComposition(context, args[0], args[1], args[2], args[3], depth);
                    }
                    else if (args.Count != 0)
                    {
                        _logger.LogWarning("CFF charstring endchar has {Count} operands, expected 0 or 4.", args.Count);
                    }

                    context.Writer.WriteRawByte(CffConstants.CharStringOperatorEndchar);
                    context.Finished = true;
                    return false;
                }
            default:
                {
                    context.Operands.Clear();
                    break;
                }
        }

        return true;
    }

    private void ApplyEscapedOperator(CffCharStringContext context, byte operatorCode)
    {
        switch (operatorCode)
        {
            case CffConstants.CharStringEscapedOperatorHflex:
                {
                    List<float> args = context.PopAll();
                    if (args.Count >= 7)
                    {
                        float dy2 = args[2];
                        EmitCurve(context, args[0], 0f, args[1], dy2, args[3], 0f);
                        EmitCurve(context, args[4], 0f, args[5], -dy2, args[6], 0f);
                    }

                    context.Writer.WriteEscapedOperator(args, CffConstants.CharStringEscapedOperatorHflex);
                    break;
                }
            case CffConstants.CharStringEscapedOperatorFlex:
                {
                    List<float> args = context.PopAll();
                    if (args.Count >= 12)
                    {
                        EmitCurve(context, args[0], args[1], args[2], args[3], args[4], args[5]);
                        EmitCurve(context, args[6], args[7], args[8], args[9], args[10], args[11]);
                    }

                    context.Writer.WriteEscapedOperator(args, CffConstants.CharStringEscapedOperatorFlex);
                    break;
                }
            case CffConstants.CharStringEscapedOperatorHflex1:
                {
                    List<float> args = context.PopAll();
                    if (args.Count >= 9)
                    {
                        float dy1 = args[1];
                        float dy2 = args[3];
                        float dy5 = args[7];
                        float dy6 = -(dy1 + dy2 + dy5);
                        EmitCurve(context, args[0], dy1, args[2], dy2, args[4], 0f);
                        EmitCurve(context, args[5], 0f, args[6], dy5, args[8], dy6);
                    }

                    context.Writer.WriteEscapedOperator(args, CffConstants.CharStringEscapedOperatorHflex1);
                    break;
                }
            case CffConstants.CharStringEscapedOperatorFlex1:
                {
                    List<float> args = context.PopAll();
                    if (args.Count >= 11)
                    {
                        float dx = args[0] + args[2] + args[4] + args[6] + args[8];
                        float dy = args[1] + args[3] + args[5] + args[7] + args[9];
                        float dx6;
                        float dy6;
                        if (MathF.Abs(dx) > MathF.Abs(dy))
                        {
                            dx6 = args[10];
                            dy6 = -dy;
                        }
                        else
                        {
                            dx6 = -dx;
                            dy6 = args[10];
                        }

                        EmitCurve(context, args[0], args[1], args[2], args[3], args[4], args[5]);
                        EmitCurve(context, args[6], args[7], args[8], args[9], dx6, dy6);
                    }

                    context.Writer.WriteEscapedOperator(args, CffConstants.CharStringEscapedOperatorFlex1);
                    break;
                }
            default:
                {
                    context.Operands.Clear();
                    break;
                }
        }
    }

    private void CallSubroutine(CffCharStringContext context, ReadOnlyMemory<byte>[] subrs, int bias, int depth)
    {
        if (context.Operands.Count == 0)
        {
            _logger.LogWarning("CFF charstring callsubr/callgsubr with no subroutine index on the stack.");
            return;
        }

        int subrIndex = (int)context.Operands[context.Operands.Count - 1] + bias;
        context.Operands.RemoveAt(context.Operands.Count - 1);

        if (subrIndex < 0 || subrIndex >= subrs.Length)
        {
            _logger.LogWarning("CFF charstring references out-of-range subroutine {SubrIndex} (of {Count}).", subrIndex, subrs.Length);
            return;
        }

        Execute(subrs[subrIndex].Span, context, depth + 1);
    }

    // Composes an accented character out of two plain glyphs, as the deprecated 4-argument form of
    // endchar: draws the base glyph unmodified, then the accent glyph shifted by (adx, ady). Unlike
    // Type1's seac, Type2 charstrings carry no side-bearing term to compensate for -- the shift is applied
    // directly. Also rewrites the composite's repacked charstring: since both components are relative-delta
    // Type2 charstrings restarting at their own local (0, 0), a corrective rmoveto bridges the base
    // component's final replay position to (adx, ady) before the accent component's own operators run.
    private void ApplySeacComposition(CffCharStringContext context, float adx, float ady, float baseCode, float accentCode, int depth)
    {
        if (!TryResolveSeacComponent(context, baseCode, out ReadOnlyMemory<byte> baseCharString)
            || !TryResolveSeacComponent(context, accentCode, out ReadOnlyMemory<byte> accentCharString))
        {
            _logger.LogWarning("CFF charstring seac-style endchar accent composition references an unresolvable base or accent glyph; glyph will be incomplete.");
            return;
        }

        context.InSeacComponent = true;
        Execute(baseCharString.Span, context, depth + 1);

        List<float> correctiveMove = new(2) { adx - context.CurrentX, ady - context.CurrentY };
        context.Writer.WriteOperator(correctiveMove, CffConstants.CharStringOperatorRmoveto);
        context.CurrentX = adx;
        context.CurrentY = ady;

        Execute(accentCharString.Span, context, depth + 1);
        context.InSeacComponent = false;
    }

    private bool TryResolveSeacComponent(CffCharStringContext context, float encodingCode, out ReadOnlyMemory<byte> componentCharString)
    {
        componentCharString = default;

        var code = (int)MathF.Round(encodingCode);
        if (code < 0 || code >= StandardEncoding.Length)
        {
            _logger.LogWarning("CFF charstring seac-style endchar references out-of-range StandardEncoding code {Code}.", code);
            return false;
        }

        PdfFontString glyphName = StandardEncoding[code];
        if (glyphName.IsEmpty || !context.NameToGid.TryGetValue(glyphName, out ushort glyphId) || glyphId >= context.CharStrings.Length)
        {
            _logger.LogWarning("CFF charstring seac-style endchar references StandardEncoding code {Code}, which could not be resolved to a glyph in this font.", code);
            return false;
        }

        componentCharString = context.CharStrings[glyphId];
        return true;
    }

    private static void EmitAlternatingCurves(CffCharStringContext context, List<float> args, bool startVertical)
    {
        int index = 0;
        bool vertical = startVertical;
        while (index + 3 < args.Count)
        {
            int remaining = args.Count - index - 4;
            float extra = (remaining == 1) ? args[index + 4] : 0f;

            if (vertical)
            {
                EmitCurve(context, 0f, args[index], args[index + 1], args[index + 2], args[index + 3], extra);
            }
            else
            {
                EmitCurve(context, args[index], 0f, args[index + 1], args[index + 2], extra, args[index + 3]);
            }

            index += (remaining == 1) ? 5 : 4;
            vertical = !vertical;
        }
    }

    private List<float> PopAllWidth(CffCharStringContext context, bool nominalCountIsOdd)
    {
        List<float> args = context.PopAll();
        if (!context.GotWidth)
        {
            bool actualCountIsOdd = (args.Count % 2) != 0;
            if (actualCountIsOdd != nominalCountIsOdd && args.Count > 0)
            {
                context.Width = context.NominalWidthX + args[0];
                args.RemoveAt(0);
            }
            else
            {
                if (actualCountIsOdd != nominalCountIsOdd)
                {
                    _logger.LogWarning("CFF charstring operator expected a width-carrying operand but the operand stack was empty; using defaultWidthX.");
                }

                context.Width = context.DefaultWidthX;
            }

            context.GotWidth = true;
        }

        return args;
    }

    private static void EmitMoveTo(CffCharStringContext context, float dx, float dy)
    {
        if (context.SawMoveTo)
        {
            context.PathBuilder.Close();
        }

        context.CurrentX += dx;
        context.CurrentY += dy;
        context.PathBuilder.MoveTo(context.CurrentX, context.CurrentY);
        context.SawMoveTo = true;
    }

    private static void EmitLine(CffCharStringContext context, float dx, float dy)
    {
        context.CurrentX += dx;
        context.CurrentY += dy;
        context.PathBuilder.LineTo(context.CurrentX, context.CurrentY);
    }

    private static void EmitCurve(CffCharStringContext context, float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
    {
        float x1 = context.CurrentX + dx1;
        float y1 = context.CurrentY + dy1;
        float x2 = x1 + dx2;
        float y2 = y1 + dy2;
        float x3 = x2 + dx3;
        float y3 = y2 + dy3;
        context.PathBuilder.CubicTo(x1, y1, x2, y2, x3, y3);
        context.CurrentX = x3;
        context.CurrentY = y3;
    }
}
