using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Reads a CFF Private DICT and produces a <see cref="CffPrivateDict"/> model.
/// </summary>
public class CffPrivateDictReader
{
    private readonly ILogger<CffPrivateDictReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffPrivateDictReader"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public CffPrivateDictReader(ILogger<CffPrivateDictReader> logger) => _logger = logger;

    /// <summary>
    /// Reads a CFF Private DICT from its raw bytes.
    /// </summary>
    /// <param name="privateDictData">The raw Private DICT bytes.</param>
    /// <returns>The parsed Private DICT.</returns>
    public CffPrivateDict Read(in ReadOnlySpan<byte> privateDictData)
    {
        CffPrivateDict result = new();
        CffDictionaryReader reader = new(privateDictData);
        List<ICffValue> operands = new(capacity: 6);

        ICffValue? value = reader.ReadNextValue();
        while (value != null)
        {
            switch (value.Type)
            {
                case CffValueType.Integer:
                case CffValueType.Real:
                    {
                        operands.Add(value);
                        break;
                    }
                case CffValueType.Operator:
                    {
                        ApplyOperator(result, value, operands);
                        operands.Clear();
                        break;
                    }
                case CffValueType.EscapedOperator:
                    {
                        // Every escaped Private DICT operator (BlueScale, BlueShift, BlueFuzz,
                        // StemSnapH/V, ForceBold, LanguageGroup, ExpansionFactor, initialRandomSeed) is
                        // hint data. Charstrings are repacked hint-free, so it is discarded rather than
                        // preserved.
                        operands.Clear();
                        break;
                    }
            }

            value = reader.ReadNextValue();
        }

        return result;
    }

    private void ApplyOperator(CffPrivateDict result, ICffValue operatorValue, List<ICffValue> operands)
    {
        byte operatorCode = ((CffValue<byte>)operatorValue).Value;

        switch (operatorCode)
        {
            case CffConstants.PrivateDictOperatorSubrs:
                {
                    result.SubrsOffset = operands.LastAsInt();
                    break;
                }
            case CffConstants.PrivateDictOperatorDefaultWidthX:
                {
                    result.DefaultWidthX = operands.LastAsFloat();
                    break;
                }
            case CffConstants.PrivateDictOperatorNominalWidthX:
                {
                    result.NominalWidthX = operands.LastAsFloat();
                    break;
                }
            default:
                {
                    // BlueValues, OtherBlues, FamilyBlues, FamilyOtherBlues, StdHW, StdVW -- all hint
                    // data, discarded for the same reason as the escaped operators above.
                    break;
                }
        }
    }
}
