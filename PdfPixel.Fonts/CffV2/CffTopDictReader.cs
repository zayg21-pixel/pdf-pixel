using Microsoft.Extensions.Logging;
using PdfPixel.Fonts.Model;
using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.CffV2;

/// <summary>
/// Reads a CFF Top DICT and produces a <see cref="CffTopDict"/> model.
/// </summary>
public class CffTopDictReader
{
    private readonly ILogger<CffTopDictReader> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CffTopDictReader"/> class.
    /// </summary>
    /// <param name="logger">Logger used for structured diagnostics during parsing.</param>
    public CffTopDictReader(ILogger<CffTopDictReader> logger) => _logger = logger;

    /// <summary>
    /// Reads a CFF Top DICT from its raw bytes.
    /// </summary>
    /// <param name="topDictData">The raw Top DICT bytes.</param>
    /// <returns>The parsed Top DICT.</returns>
    public CffTopDict Read(in ReadOnlySpan<byte> topDictData)
    {
        CffTopDict result = new();
        CffDictionaryReader reader = new(topDictData);
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
                        ApplyEscapedOperator(result, value, operands);
                        operands.Clear();
                        break;
                    }
            }

            value = reader.ReadNextValue();
        }

        return result;
    }

    private void ApplyOperator(CffTopDict result, ICffValue operatorValue, List<ICffValue> operands)
    {
        byte operatorCode = ((CffValue<byte>)operatorValue).Value;

        switch (operatorCode)
        {
            case CffConstants.TopDictOperatorWeight:
                {
                    result.Weight = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictOperatorFontBBox:
                {
                    if (operands.Count >= 4)
                    {
                        result.FontBBox = operands.ToFloatArray();
                    }
                    else
                    {
                        _logger.LogWarning("CFF Top DICT FontBBox operator has {Count} operands, expected 4.", operands.Count);
                    }

                    break;
                }
            case CffConstants.TopDictOperatorCharset:
                {
                    result.CharsetOffset = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictOperatorEncoding:
                {
                    result.EncodingOffset = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictOperatorCharStrings:
                {
                    result.CharStringsOffset = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictOperatorPrivate:
                {
                    if (operands.Count >= 2)
                    {
                        result.PrivateDictSize = operands[0].AsInt();
                        result.PrivateDictOffset = operands[1].AsInt();
                    }
                    else
                    {
                        _logger.LogWarning("CFF Top DICT Private operator has {Count} operands, expected 2.", operands.Count);
                    }

                    break;
                }
            default:
                {
                    StoreInformational(result, operatorValue, operands);
                    break;
                }
        }
    }

    private void ApplyEscapedOperator(CffTopDict result, ICffValue operatorValue, List<ICffValue> operands)
    {
        byte operatorCode = ((CffValue<byte>)operatorValue).Value;

        switch (operatorCode)
        {
            case CffConstants.TopDictEscapedOperatorItalicAngle:
                {
                    result.ItalicAngle = operands.LastAsFloat();
                    break;
                }
            case CffConstants.TopDictEscapedOperatorPaintType:
                {
                    result.PaintType = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictEscapedOperatorCharstringType:
                {
                    result.CharstringType = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictEscapedOperatorFontMatrix:
                {
                    if (operands.Count >= 6)
                    {
                        result.FontMatrix = PdfFontMatrix.FromArray(operands.ToFloatArray());
                    }
                    else
                    {
                        _logger.LogWarning("CFF Top DICT FontMatrix operator has {Count} operands, expected 6.", operands.Count);
                    }

                    break;
                }
            case CffConstants.TopDictEscapedOperatorStrokeWidth:
                {
                    result.StrokeWidth = operands.LastAsFloat();
                    break;
                }
            case CffConstants.TopDictEscapedOperatorRos:
                {
                    if (operands.Count >= 3)
                    {
                        result.Ros = new CffRos(operands[0].AsInt(), operands[1].AsInt(), operands[2].AsFloat());
                    }
                    else
                    {
                        _logger.LogWarning("CFF Top DICT ROS operator has {Count} operands, expected 3.", operands.Count);
                    }

                    break;
                }
            case CffConstants.TopDictEscapedOperatorFdArray:
                {
                    result.FdArrayOffset = operands.LastAsInt();
                    break;
                }
            case CffConstants.TopDictEscapedOperatorFdSelect:
                {
                    result.FdSelectOffset = operands.LastAsInt();
                    break;
                }
            default:
                {
                    StoreInformational(result, operatorValue, operands);
                    break;
                }
        }
    }

    private static void StoreInformational(CffTopDict result, ICffValue operatorValue, List<ICffValue> operands)
        => result.Entries.Add(new CffDictEntry(operatorValue, operands.ToArray()));
}
