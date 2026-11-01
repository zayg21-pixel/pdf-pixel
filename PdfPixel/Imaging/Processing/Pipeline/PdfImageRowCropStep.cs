using PdfPixel.Geometry;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Processing.Pipeline;

/// <summary>
/// Takes a rectangular region out of packed sample rows, returning its rows starting at bit 0 and
/// numbered from the top of the region.
/// </summary>
public sealed class PdfImageRowCropStep : IPdfImageRowProcessingStep
{
    private readonly PdfIntegerRectangle _region;
    private readonly int _startBit;
    private readonly int _rowBytes;
    private readonly int _requiredInputBits;
    private readonly byte[]? _buffer;

    /// <summary>
    /// Initializes a step taking <paramref name="region"/> out of rows of packed samples.
    /// </summary>
    /// <param name="bitsPerComponent">Bit depth of each sample.</param>
    /// <param name="componentCount">Number of components in each pixel.</param>
    /// <param name="region">Region to take, in pixels and rows of the input.</param>
    public PdfImageRowCropStep(int bitsPerComponent, int componentCount, in PdfIntegerRectangle region)
    {
        if (bitsPerComponent < 1 || bitsPerComponent > 16)
        {
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 1 to 16.");
        }

        if (componentCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCount), "Component count must be positive.");
        }

        if (region.Left < 0 || region.Top < 0 || region.Width <= 0 || region.Height <= 0)
        {
            throw new ArgumentException("The region must be non-empty and start at a non-negative pixel and row.", nameof(region));
        }

        int bitsPerPixel = bitsPerComponent * componentCount;

        _region = region;
        _startBit = region.Left * bitsPerPixel;
        _rowBytes = ((region.Width * bitsPerPixel) + 7) / 8;
        _requiredInputBits = _startBit + (region.Width * bitsPerPixel);

        if ((_startBit & 7) != 0)
        {
            _buffer = new byte[_rowBytes];
        }
    }

    /// <inheritdoc />
#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    public PdfImageRow Process(ReadOnlySpan<byte> input, int inputRowIndex)
    {
        if ((long)input.Length * 8 < _requiredInputBits)
        {
            throw new ArgumentException("The input row is shorter than the region.", nameof(input));
        }

        if (inputRowIndex < _region.Top || inputRowIndex >= _region.Bottom)
        {
            return PdfImageRow.Empty;
        }

        if (_buffer == null)
        {
            return new PdfImageRow(input.Slice(_startBit >> 3, _rowBytes), inputRowIndex - _region.Top, hasNextRow: false);
        }
        else
        {
            ShiftToStart(input, _buffer);
            return new PdfImageRow(_buffer, inputRowIndex - _region.Top, hasNextRow: false);
        }
    }

#if NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
#else
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
#endif
    private void ShiftToStart(in ReadOnlySpan<byte> input, in Span<byte> destination)
    {
        int sourceByte = _startBit >> 3;
        int shift = _startBit & 7;
        int inverseShift = 8 - shift;
        int lastFullByte = Math.Min(_rowBytes, input.Length - sourceByte - 1);

        for (int index = 0; index < lastFullByte; index++)
        {
            destination[index] = (byte)((input[sourceByte + index] << shift) | (input[sourceByte + index + 1] >> inverseShift));
        }

        for (int index = lastFullByte; index < _rowBytes; index++)
        {
            destination[index] = (byte)(input[sourceByte + index] << shift);
        }
    }
}
