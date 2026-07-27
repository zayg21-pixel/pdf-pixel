using System;
using System.Collections.Generic;

namespace PdfPixel.Fonts.Cff;

/// <summary>
/// Writes Type2 charstring operators and operands into its own accumulated byte buffer, used by
/// <see cref="CffCharStringEvaluator"/> to emit its repacked, hint-free, subroutine-free charstring output.
/// </summary>
internal sealed class CffCharStringWriter
{
    private readonly List<byte> _output = [];

    public void WriteOperator(List<float> args, byte operatorCode)
    {
        foreach (float value in args)
        {
            WriteNumber(value);
        }

        _output.Add(operatorCode);
    }

    public void WriteEscapedOperator(List<float> args, byte operatorCode)
    {
        foreach (float value in args)
        {
            WriteNumber(value);
        }

        _output.Add(CffConstants.DictOperatorEscape);
        _output.Add(operatorCode);
    }

    public void WriteRawByte(byte value) => _output.Add(value);

    /// <summary>
    /// Inserts a width value at the very start of the accumulated output, ahead of everything already
    /// written, as its own <c>hstem</c> operator carrying one degenerate zero-width stem pair alongside
    /// the width.
    /// </summary>
    public void PrependWidth(float value)
    {
        CffCharStringWriter prefixWriter = new();
        prefixWriter.WriteNumber(value);
        prefixWriter.WriteNumber(0f);
        prefixWriter.WriteNumber(0f);
        prefixWriter.WriteRawByte(CffConstants.CharStringOperatorHstem);
        _output.InsertRange(0, prefixWriter._output);
    }

    public byte[] ToArray() => _output.ToArray();

    public void WriteNumber(float value)
    {
        bool isInteger = value == MathF.Round(value) && value >= -32768f && value <= 32767f;
        if (isInteger)
        {
            WriteInteger((int)value);
        }
        else
        {
            WriteFixed(value);
        }
    }

    private void WriteInteger(int value)
    {
        if (value >= -107 && value <= 107)
        {
            _output.Add((byte)(value + CffConstants.SingleByteIntegerBias));
            return;
        }

        if (value >= 108 && value <= 1131)
        {
            int adjusted = value - CffConstants.TwoByteIntegerBias;
            _output.Add((byte)(CffConstants.OperandPositiveIntStart + (adjusted >> 8)));
            _output.Add((byte)(adjusted & 0xFF));
            return;
        }

        if (value >= -1131 && value <= -108)
        {
            int adjusted = -value - CffConstants.TwoByteIntegerBias;
            _output.Add((byte)(CffConstants.OperandNegativeIntStart + (adjusted >> 8)));
            _output.Add((byte)(adjusted & 0xFF));
            return;
        }

        _output.Add(CffConstants.OperandShortInt);
        _output.Add((byte)((value >> 8) & 0xFF));
        _output.Add((byte)(value & 0xFF));
    }

    private void WriteFixed(float value)
    {
        var fixedValue = (int)MathF.Round(value * 65536f);
        _output.Add(CffConstants.CharStringOperandFixed);
        _output.Add((byte)((fixedValue >> 24) & 0xFF));
        _output.Add((byte)((fixedValue >> 16) & 0xFF));
        _output.Add((byte)((fixedValue >> 8) & 0xFF));
        _output.Add((byte)(fixedValue & 0xFF));
    }
}
