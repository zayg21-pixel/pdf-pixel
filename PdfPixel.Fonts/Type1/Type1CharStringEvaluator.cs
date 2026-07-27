using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Cff;
using PdfPixel.Fonts.Model;
using PdfPixel.Fonts.Resources;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Type1;

/// <summary>
/// Evaluates a Type1 charstring into a <see cref="CffCharacter"/>: inlines <c>callsubr</c> and <c>seac</c>
/// accent composition, resolves flex (<c>callothersubr</c> 0/1/2) into plain curves, drops hint operators
/// (<c>hstem</c>/<c>vstem</c>/<c>vstem3</c>/<c>hstem3</c>/<c>dotsection</c>) entirely, builds the outline
/// via <see cref="PdfFontPathBuilder"/>, and emits a flattened, hint-free Type2 charstring alongside the
/// extracted width -- the same approach and output classes (<see cref="CffCharStringWriter"/>,
/// <see cref="CffConstants"/>) that <see cref="CffCharStringEvaluator"/> uses for Type2 charstrings.
/// </summary>
internal sealed class Type1CharStringEvaluator
{
    // Type1-only operator codes -- CffConstants only defines the ones Type1 shares with Type2.
    private const byte OpClosePath = 9;
    private const byte OpHsbw = 13;
    private const byte EscDotSection = 0;
    private const byte EscVstem3 = 1;
    private const byte EscHstem3 = 2;
    private const byte EscSeac = 6;
    private const byte EscSbw = 7;
    private const byte EscDiv = 12;
    private const byte EscCallOtherSubr = 16;
    private const byte EscPop = 17;
    private const byte EscSetCurrentPoint = 33;

    private const int OtherSubrStartFlex = 1;
    private const int OtherSubrAddFlexPoint = 2;
    private const int OtherSubrEndFlex = 0;
    private const int OtherSubrHintReplacement = 3;

    private static readonly PdfFontString[] StandardEncoding = SingleByteEncodings.GetEncodingSet(PdfFontEncoding.StandardEncoding) ?? Array.Empty<PdfFontString>();

    private readonly ILogger<Type1CharStringEvaluator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="Type1CharStringEvaluator"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during evaluation.</param>
    public Type1CharStringEvaluator(ILogger<Type1CharStringEvaluator> logger) => _logger = logger;

    /// <summary>
    /// Evaluates a single Type1 charstring.
    /// </summary>
    /// <param name="charString">The raw (decrypted) Type1 charstring bytes.</param>
    /// <param name="localSubrs">The font's decrypted local subroutines, by index.</param>
    /// <param name="charStrings">This font's raw Type1 charstrings by glyph name -- used to resolve the
    /// base and accent glyphs of a <c>seac</c> accent composition.</param>
    /// <param name="matrix">The font's FontMatrix, applied to every outline point.</param>
    /// <returns>The evaluated character.</returns>
    public CffCharacter Evaluate(
        in ReadOnlyMemory<byte> charString,
        IReadOnlyDictionary<int, byte[]> localSubrs,
        IReadOnlyDictionary<PdfFontString, byte[]> charStrings,
        in PdfFontMatrix matrix)
    {
        if (localSubrs == null)
        {
            throw new ArgumentNullException(nameof(localSubrs));
        }

        if (charStrings == null)
        {
            throw new ArgumentNullException(nameof(charStrings));
        }

        Type1CharStringContext context = new(localSubrs, charStrings, matrix);
        Execute(charString.Span, context, depth: 0);

        if (context.SawMoveTo)
        {
            context.PathBuilder.Close();
        }

        if (context.Width != 0f)
        {
            context.Writer.PrependWidth(context.Width);
        }

        return new CffCharacter(context.PathBuilder.ToPath(), context.Writer.ToArray(), context.Width, dict: null);
    }

    private void Execute(in ReadOnlySpan<byte> data, Type1CharStringContext context, int depth)
    {
        if (depth > CffConstants.MaxSubroutineNestingDepth)
        {
            _logger.LogWarning("Type1 charstring exceeded max subroutine/seac nesting depth ({MaxDepth}); aborting this branch.", CffConstants.MaxSubroutineNestingDepth);
            return;
        }

        Type1CharStringReader reader = new(data);
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
                    if (!ApplyOperator(context, operatorCode, depth))
                    {
                        return;
                    }

                    break;
                }
                case CffValueType.EscapedOperator:
                {
                    byte operatorCode = ((CffValue<byte>)value).Value;
                    ApplyEscapedOperator(context, operatorCode, depth);
                    break;
                }
            }

            value = reader.ReadNextValue();
        }
    }

    // Returns false when this frame must stop processing further bytes (return/endchar).
    private bool ApplyOperator(Type1CharStringContext context, byte operatorCode, int depth)
    {
        switch (operatorCode)
        {
            case CffConstants.CharStringOperatorHstem:
            case CffConstants.CharStringOperatorVstem:
            {
                context.Operands.Clear(); // Hints dropped entirely.
                break;
            }
            case OpClosePath:
            {
                context.Operands.Clear();
                break;
            }
            case OpHsbw:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 2)
                {
                    SetSideBearing(context, args[0], 0f, args[1]);
                }

                break;
            }
            case CffConstants.CharStringOperatorRmoveto:
            {
                List<float> args = context.PopAll();
                float dx = (args.Count > 0) ? args[0] : 0f;
                float dy = (args.Count > 1) ? args[1] : 0f;
                HandleMoveTo(context, dx, dy);
                break;
            }
            case CffConstants.CharStringOperatorHmoveto:
            {
                List<float> args = context.PopAll();
                float dx = (args.Count > 0) ? args[0] : 0f;
                HandleMoveTo(context, dx, 0f);
                break;
            }
            case CffConstants.CharStringOperatorVmoveto:
            {
                List<float> args = context.PopAll();
                float dy = (args.Count > 0) ? args[0] : 0f;
                HandleMoveTo(context, 0f, dy);
                break;
            }
            case CffConstants.CharStringOperatorRlineto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 2)
                {
                    EmitLine(context, args[0], args[1]);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRlineto);
                break;
            }
            case CffConstants.CharStringOperatorHlineto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 1)
                {
                    EmitLine(context, args[0], 0f);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorHlineto);
                break;
            }
            case CffConstants.CharStringOperatorVlineto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 1)
                {
                    EmitLine(context, 0f, args[0]);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorVlineto);
                break;
            }
            case CffConstants.CharStringOperatorRrcurveto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 6)
                {
                    EmitCurve(context, args[0], args[1], args[2], args[3], args[4], args[5]);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorRrcurveto);
                break;
            }
            case CffConstants.CharStringOperatorVhcurveto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 4)
                {
                    EmitCurve(context, 0f, args[0], args[1], args[2], args[3], 0f);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorVhcurveto);
                break;
            }
            case CffConstants.CharStringOperatorHvcurveto:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 4)
                {
                    EmitCurve(context, args[0], 0f, args[1], args[2], 0f, args[3]);
                }

                context.Writer.WriteOperator(args, CffConstants.CharStringOperatorHvcurveto);
                break;
            }
            case CffConstants.CharStringOperatorCallsubr:
            {
                if (context.Operands.Count == 0)
                {
                    _logger.LogWarning("Type1 charstring callsubr with no subroutine index on the stack.");
                    break;
                }

                var subrIndex = (int)context.Operands[context.Operands.Count - 1];
                context.Operands.RemoveAt(context.Operands.Count - 1);

                if (!context.LocalSubrs.TryGetValue(subrIndex, out byte[]? subrBytes))
                {
                    _logger.LogWarning("Type1 charstring references unknown local subroutine {SubrIndex}.", subrIndex);
                    break;
                }

                Execute(subrBytes, context, depth + 1);
                break;
            }
            case CffConstants.CharStringOperatorReturn:
            {
                return false;
            }
            case CffConstants.CharStringOperatorEndchar:
            {
                if (context.InSeacComponent)
                {
                    return false;
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

    private void ApplyEscapedOperator(Type1CharStringContext context, byte operatorCode, int depth)
    {
        switch (operatorCode)
        {
            case EscDotSection:
            case EscVstem3:
            case EscHstem3:
            {
                context.Operands.Clear(); // Hints dropped entirely.
                break;
            }
            case EscSbw:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 4)
                {
                    SetSideBearing(context, args[0], args[1], args[2]);
                }

                break;
            }
            case EscSeac:
            {
                List<float> args = context.PopAll();
                if (args.Count >= 5)
                {
                    ApplySeacComposition(context, args[0], args[1], args[2], args[3], args[4], depth);
                }

                break;
            }
            case EscDiv:
            {
                if (context.Operands.Count < 2)
                {
                    _logger.LogWarning("Type1 charstring div with fewer than 2 operands on the stack.");
                    break;
                }

                float divisor = context.Operands[context.Operands.Count - 1];
                float dividend = context.Operands[context.Operands.Count - 2];
                context.Operands.RemoveAt(context.Operands.Count - 1);
                context.Operands.RemoveAt(context.Operands.Count - 1);
                context.Operands.Add(dividend / divisor);
                break;
            }
            case EscCallOtherSubr:
            {
                ApplyCallOtherSubr(context);
                break;
            }
            case EscPop:
            {
                if (context.OtherSubrResults.Count > 0)
                {
                    context.Operands.Add(context.OtherSubrResults[context.OtherSubrResults.Count - 1]);
                    context.OtherSubrResults.RemoveAt(context.OtherSubrResults.Count - 1);
                }
                else
                {
                    context.Operands.Add(0f);
                }

                break;
            }
            case EscSetCurrentPoint:
            {
                context.Operands.Clear();
                break;
            }
            default:
            {
                context.Operands.Clear();
                break;
            }
        }
    }

    private void ApplyCallOtherSubr(Type1CharStringContext context)
    {
        if (context.Operands.Count == 0)
        {
            return;
        }

        var otherSubrIndex = (int)context.Operands[context.Operands.Count - 1];
        context.Operands.RemoveAt(context.Operands.Count - 1);
        if (context.Operands.Count > 0)
        {
            context.Operands.RemoveAt(context.Operands.Count - 1); // Argument count, always known for the flex othersubrs handled here.
        }

        switch (otherSubrIndex)
        {
            case OtherSubrStartFlex:
            {
                context.InFlexSequence = true;
                context.FlexDeltas = new List<float>(14);
                context.Operands.Clear();
                break;
            }
            case OtherSubrAddFlexPoint:
            {
                context.Operands.Clear();
                break;
            }
            case OtherSubrEndFlex:
            {
                if (context.FlexDeltas != null && context.FlexDeltas.Count >= 14)
                {
                    List<float> curveArgs = MergeFlexReferencePoint(context.FlexDeltas);
                    EmitCurve(context, curveArgs[0], curveArgs[1], curveArgs[2], curveArgs[3], curveArgs[4], curveArgs[5]);
                    EmitCurve(context, curveArgs[6], curveArgs[7], curveArgs[8], curveArgs[9], curveArgs[10], curveArgs[11]);
                    context.Writer.WriteOperator(curveArgs, CffConstants.CharStringOperatorRrcurveto);
                }

                context.FlexDeltas = null;
                context.InFlexSequence = false;
                context.Operands.Clear();
                break;
            }
            case OtherSubrHintReplacement:
            {
                if (context.Operands.Count > 0)
                {
                    context.OtherSubrResults.Add(context.Operands[context.Operands.Count - 1]);
                }

                context.Operands.Clear();
                break;
            }
            default:
            {
                context.Operands.Clear();
                break;
            }
        }
    }

    private void ApplySeacComposition(Type1CharStringContext context, float asb, float adx, float ady, float baseCode, float accentCode, int depth)
    {
        if (!TryResolveSeacComponent(context, baseCode, out byte[] baseCharString) || !TryResolveSeacComponent(context, accentCode, out byte[] accentCharString))
        {
            _logger.LogWarning("Type1 charstring seac references an unresolvable base or accent glyph; glyph will be incomplete.");
            return;
        }

        float compositeWidth = context.Width;
        float compositeSideBearingX = context.SideBearingX;

        context.InSeacComponent = true;
        Execute(baseCharString, context, depth + 1);

        // The accent's own hsbw/sbw repositions it to (its own side bearing + this offset): together
        // that reproduces the spec's "(adx - asb + sb, ady)", where sb is the composite's own side bearing.
        context.SeacOffsetX = adx - asb + compositeSideBearingX;
        context.SeacOffsetY = ady;
        Execute(accentCharString, context, depth + 1);
        context.SeacOffsetX = 0f;
        context.SeacOffsetY = 0f;

        context.InSeacComponent = false;
        context.Width = compositeWidth;
    }

    private bool TryResolveSeacComponent(Type1CharStringContext context, float encodingCode, out byte[] componentCharString)
    {
        componentCharString = Array.Empty<byte>();

        var code = (int)MathF.Round(encodingCode);
        if (code < 0 || code >= StandardEncoding.Length)
        {
            _logger.LogWarning("Type1 charstring seac references out-of-range StandardEncoding code {Code}.", code);
            return false;
        }

        PdfFontString glyphName = StandardEncoding[code];
        if (glyphName.IsEmpty || !context.CharStrings.TryGetValue(glyphName, out byte[]? resolvedCharString))
        {
            _logger.LogWarning("Type1 charstring seac references StandardEncoding code {Code}, which could not be resolved to a glyph in this font.", code);
            return false;
        }

        componentCharString = resolvedCharString;
        return true;
    }

    /// <summary>
    /// Merges the flex reference point (first dx/dy pair) into the first curve point so the result
    /// contains exactly 12 values (two valid rrcurveto curves of 6 args each). If the list doesn't have
    /// the expected 14 values it is returned unchanged.
    /// </summary>
    private static List<float> MergeFlexReferencePoint(List<float> flexDeltas)
    {
        // Standard flex: 7 points x 2 coords = 14 values.
        // Points: ref(0,1) p1(2,3) p2(4,5) p3(6,7) p4(8,9) p5(10,11) p6(12,13)
        // Merge ref into p1 so rrcurveto sees 12 args: (ref+p1), p2, p3 | p4, p5, p6.
        if (flexDeltas.Count < 14)
        {
            return flexDeltas;
        }

        List<float> merged = new(12)
        {
            flexDeltas[0] + flexDeltas[2],
            flexDeltas[1] + flexDeltas[3]
        };

        for (int index = 4; index < flexDeltas.Count; index++)
        {
            merged.Add(flexDeltas[index]);
        }

        return merged;
    }

    // hsbw/sbw reposition the current point without drawing anything themselves; the shift is folded
    // into the next real moveto's own delta by HandleMoveTo.
    private static void SetSideBearing(Type1CharStringContext context, float sidebearingX, float sidebearingY, float width)
    {
        context.SideBearingX = sidebearingX;
        context.Width = width;
        context.PendingOriginDeltaX = sidebearingX + context.SeacOffsetX - context.CurrentX;
        context.PendingOriginDeltaY = sidebearingY + context.SeacOffsetY - context.CurrentY;
    }

    private static void HandleMoveTo(Type1CharStringContext context, float dx, float dy)
    {
        if (context.InFlexSequence)
        {
            (context.FlexDeltas ??= new List<float>(14)).Add(dx);
            context.FlexDeltas.Add(dy);
            return;
        }

        float effectiveDx = dx + context.PendingOriginDeltaX;
        float effectiveDy = dy + context.PendingOriginDeltaY;
        context.PendingOriginDeltaX = 0f;
        context.PendingOriginDeltaY = 0f;

        EmitMoveTo(context, effectiveDx, effectiveDy);
        context.Writer.WriteOperator(new List<float> { effectiveDx, effectiveDy }, CffConstants.CharStringOperatorRmoveto);
    }

    private static void EmitMoveTo(Type1CharStringContext context, float dx, float dy)
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

    private static void EmitLine(Type1CharStringContext context, float dx, float dy)
    {
        context.CurrentX += dx;
        context.CurrentY += dy;
        context.PathBuilder.LineTo(context.CurrentX, context.CurrentY);
    }

    private static void EmitCurve(Type1CharStringContext context, float dx1, float dy1, float dx2, float dy2, float dx3, float dy3)
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
