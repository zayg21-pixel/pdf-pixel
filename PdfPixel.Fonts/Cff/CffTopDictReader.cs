using System;
using Microsoft.Extensions.Logging;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// High-level reader for CFF Top DICT structures.
/// Provides a clean API to extract Top DICT metadata.
/// </summary>
internal sealed class CffTopDictReader
{
    private const byte OperatorEscapedRos = 30;
    private const byte OperatorEscapedFontMatrix = 7;
    private const byte OperatorCharset = 15;
    private const byte OperatorEncoding = 16;
    private const byte OperatorCharStrings = 17;
    private const byte OperatorPrivate = 18;

    private readonly ILogger _logger;

    public CffTopDictReader(ILogger logger) => _logger = logger;

    /// <summary>
    /// Parses a CFF Top DICT and extracts relevant metadata.
    /// Caller should check if CharStringsOffset is zero to determine if parsing was successful.
    /// </summary>
    /// <param name="topDictBytes">The Top DICT data bytes.</param>
    /// <returns>Parsed Top DICT data (never null).</returns>
    public CffTopDict ParseTopDict(in ReadOnlySpan<byte> topDictBytes)
    {
        CffTopDict result = new();
        CffDictionaryReader dictReader = new(topDictBytes, _logger);

        while (dictReader.TryReadNextOperator(out byte @operator, out decimal[] operands))
        {
            switch (@operator)
            {
                case OperatorCharset:
                    {
                        if (operands.Length > 0)
                        {
                            result.CharsetOffset = (int)operands[operands.Length - 1];
                        }

                        break;
                    }
                case OperatorEncoding:
                    {
                        if (operands.Length > 0)
                        {
                            result.EncodingOffset = (int)operands[operands.Length - 1];
                        }

                        break;
                    }
                case OperatorCharStrings:
                    {
                        if (operands.Length > 0)
                        {
                            result.CharStringsOffset = (int)operands[operands.Length - 1];
                        }

                        break;
                    }
                case OperatorPrivate:
                    {
                        if (operands.Length >= 2)
                        {
                            result.PrivateDictSize = (int)operands[0];
                            result.PrivateDictOffset = (int)operands[1];
                        }

                        break;
                    }
                case OperatorEscapedRos:
                    {
                        result.IsCidKeyed = true;
                        break;
                    }
                case OperatorEscapedFontMatrix:
                    {
                        if (operands.Length >= 6)
                        {
                            result.FontMatrix = operands;
                        }

                        break;
                    }
            }
        }

        return result;
    }
}
