using System;
using Microsoft.Extensions.Logging;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// High-level reader for CFF Private DICT structures.
/// Provides a clean API to extract Private DICT metadata.
/// </summary>
internal sealed class CffPrivateDictParser
{
    private const byte OperatorSubrs = 19;
    private const byte OperatorDefaultWidthX = 20;
    private const byte OperatorNominalWidthX = 21;

    private readonly ILogger _logger;

    public CffPrivateDictParser(ILogger logger) => _logger = logger;

    /// <summary>
    /// Parses a CFF Private DICT and extracts relevant metadata.
    /// Parses what it can find and fills the data class.
    /// </summary>
    /// <param name="privateDictBytes">The Private DICT data bytes.</param>
    /// <returns>Parsed Private DICT data (never null).</returns>
    public CffPrivateDictData ParsePrivateDict(in ReadOnlySpan<byte> privateDictBytes)
    {
        CffPrivateDictData data = new();
        CffDictionaryReader dictReader = new(privateDictBytes, _logger);

        while (dictReader.TryReadNextOperator(out byte @operator, out decimal[] operands))
        {
            switch (@operator)
            {
                case OperatorSubrs:
                    {
                        if (operands.Length > 0)
                        {
                            data.SubrsOffset = (int)operands[operands.Length - 1];
                        }

                        break;
                    }
                case OperatorDefaultWidthX:
                    {
                        if (operands.Length > 0)
                        {
                            data.DefaultWidthX = (double)operands[operands.Length - 1];
                        }

                        break;
                    }
                case OperatorNominalWidthX:
                    {
                        if (operands.Length > 0)
                        {
                            data.NominalWidthX = (double)operands[operands.Length - 1];
                        }

                        break;
                    }
            }
        }

        return data;
    }
}
