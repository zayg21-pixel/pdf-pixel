using PdfPixel.Tiff.Model;
using System;
using System.IO;

namespace PdfPixel.Tiff.Decoding;

/// <summary>
/// Decodes a TIFF image row by row into interleaved unsigned samples of <see cref="BitsPerComponent"/> bits,
/// packed MSB-first. Floating-point samples are clamped to 0 to 1 and YCbCr samples are converted to RGB.
/// </summary>
public sealed partial class TiffRowDecoder
{
    private const int MaxBitsPerComponent = 16;
    private const int MaxBitsPerSample = 32;
    private const double MaxFloatOutput = 65535;

    private static readonly int[] DataUnitSampleBits = [8];
    private static readonly int[] DefaultYCbCrSubsampling = [2, 2];

    private readonly ReadOnlyMemory<byte> _data;
    private readonly TiffChunkDecoder _chunkDecoder;
    private readonly long[] _chunkOffsets;
    private readonly long[] _chunkByteCounts;
    private readonly bool _isTiled;
    private readonly int _planeCount;
    private readonly int _chunkWidth;
    private readonly int _chunkHeight;
    private readonly int _chunksAcross;
    private readonly int _chunksPerPlane;

    /// <summary>
    /// Bit depth of each stored sample.
    /// </summary>
    private readonly int[] _sampleBits;

    /// <summary>
    /// Bit offset of each sample within the stored pixel of its plane.
    /// </summary>
    private readonly int[] _sampleBitOffsets;

    /// <summary>
    /// Sign bit of each stored sample for two's complement data, zero for unsigned data.
    /// </summary>
    private readonly uint[] _sampleSignBits;

    /// <summary>
    /// Right shift from each sample's depth down to <see cref="BitsPerComponent"/>.
    /// </summary>
    private readonly int[] _sampleRightShifts;

    /// <summary>
    /// Left shift from each sample's depth up to <see cref="BitsPerComponent"/>.
    /// </summary>
    private readonly int[] _sampleLeftShifts;

    /// <summary>
    /// Depths of the samples stored in the chunks of each plane.
    /// </summary>
    private readonly int[][] _planeSampleBits;

    private readonly int[] _planeBitsPerPixel;
    private readonly int[] _planeChunkRowBytes;
    private readonly byte[][] _chunkBuffers;

    /// <summary>
    /// Stored row of each plane, or null when stored rows already are output rows or are read as data units.
    /// </summary>
    private readonly byte[][]? _storedRows;

    private readonly bool _isFloat;

    /// <summary>
    /// Converter of YCbCr samples outside JPEG, or null for every other color model.
    /// </summary>
    private readonly TiffYCbCrConverter? _yCbCrConverter;

    /// <summary>
    /// Whether chunks hold chunky YCbCr data units: per block of subsampled pixels, the luma samples then Cb and Cr.
    /// </summary>
    private readonly bool _hasDataUnits;

    private readonly int _subsamplingHorizontal = 1;
    private readonly int _subsamplingVertical = 1;

    private int _loadedChunkRow = -1;

    /// <summary>
    /// Initializes a decoder for the image a directory describes.
    /// </summary>
    /// <param name="directory">Directory of the image to decode.</param>
    /// <param name="data">Complete TIFF file the directory was read from.</param>
    public TiffRowDecoder(TiffDirectory directory, in ReadOnlyMemory<byte> data)
    {
        if (directory == null)
        {
            throw new ArgumentNullException(nameof(directory));
        }

        _data = data;

        Width = directory.Width;
        Height = directory.Height;
        ComponentCount = directory.SamplesPerPixel;
        Photometric = (directory.Photometric == TiffPhotometric.YCbCr)
            ? TiffPhotometric.Rgb
            : directory.Photometric;

        _sampleBits = ResolveSampleBits(directory);
        Validate(directory, _sampleBits);

        int maximumSampleBits = 0;
        for (int sample = 0; sample < ComponentCount; sample++)
        {
            maximumSampleBits = Math.Max(maximumSampleBits, _sampleBits[sample]);
        }

        _isFloat = directory.SampleFormat == TiffSampleFormat.FloatingPoint;
        BitsPerComponent = _isFloat ? MaxBitsPerComponent : Math.Min(MaxBitsPerComponent, maximumSampleBits);
        RowBytes = ((Width * ComponentCount * BitsPerComponent) + 7) / 8;

        bool isPlanar = directory.PlanarConfiguration == TiffPlanarConfiguration.Planar && ComponentCount > 1;
        bool isSigned = directory.SampleFormat == TiffSampleFormat.SignedInteger;
        _planeCount = isPlanar ? ComponentCount : 1;

        if (directory.Photometric == TiffPhotometric.YCbCr && !IsJpeg(directory.Compression))
        {
            _yCbCrConverter = new TiffYCbCrConverter(directory);
            _hasDataUnits = !isPlanar;

            if (_hasDataUnits)
            {
                int[] subsampling = directory.YCbCrSubsampling ?? DefaultYCbCrSubsampling;
                _subsamplingHorizontal = subsampling[0];
                _subsamplingVertical = subsampling[1];
            }
        }

        _sampleBitOffsets = new int[ComponentCount];
        _sampleSignBits = new uint[ComponentCount];
        _sampleRightShifts = new int[ComponentCount];
        _sampleLeftShifts = new int[ComponentCount];

        bool isStoredAsOutput = !isPlanar && !isSigned && !_isFloat && _yCbCrConverter == null;
        int chunkyBitOffset = 0;

        for (int sample = 0; sample < ComponentCount; sample++)
        {
            int bits = _sampleBits[sample];
            int shift = bits - BitsPerComponent;

            _sampleBitOffsets[sample] = isPlanar ? 0 : chunkyBitOffset;
            _sampleSignBits[sample] = isSigned ? 1u << (bits - 1) : 0;
            _sampleRightShifts[sample] = Math.Max(shift, 0);
            _sampleLeftShifts[sample] = Math.Max(-shift, 0);

            chunkyBitOffset += bits;
            isStoredAsOutput &= bits == BitsPerComponent;
        }

        _planeSampleBits = new int[_planeCount][];
        _planeBitsPerPixel = new int[_planeCount];
        for (int plane = 0; plane < _planeCount; plane++)
        {
            _planeSampleBits[plane] = isPlanar ? [_sampleBits[plane]] : _sampleBits;
            _planeBitsPerPixel[plane] = isPlanar ? _sampleBits[plane] : chunkyBitOffset;
        }

        _isTiled = directory.TileWidth.HasValue && directory.TileHeight.HasValue;
        if (_isTiled)
        {
            _chunkWidth = directory.TileWidth ?? 0;
            _chunkHeight = directory.TileHeight ?? 0;
            _chunkOffsets = directory.TileOffsets ?? throw new InvalidDataException("Tiled TIFF image has no TileOffsets.");
            _chunkByteCounts = directory.TileByteCounts ?? throw new InvalidDataException("Tiled TIFF image has no TileByteCounts.");
        }
        else
        {
            long rowsPerStrip = directory.RowsPerStrip ?? Height;
            _chunkWidth = Width;
            _chunkHeight = (int)Math.Min(Math.Max(rowsPerStrip, 1), Height);
            _chunkOffsets = directory.StripOffsets ?? throw new InvalidDataException("TIFF image has no StripOffsets.");
            _chunkByteCounts = directory.StripByteCounts ?? throw new InvalidDataException("TIFF image has no StripByteCounts.");
        }

        if (_chunkWidth <= 0 || _chunkHeight <= 0)
        {
            throw new InvalidDataException($"TIFF chunk size {_chunkWidth}x{_chunkHeight} is invalid.");
        }

        _chunksAcross = (Width + _chunkWidth - 1) / _chunkWidth;
        int chunksDown = (Height + _chunkHeight - 1) / _chunkHeight;
        _chunksPerPlane = _chunksAcross * chunksDown;

        if (_chunkOffsets.Length < _chunksPerPlane * _planeCount || _chunkByteCounts.Length < _chunksPerPlane * _planeCount)
        {
            throw new InvalidDataException($"TIFF image lists {_chunkOffsets.Length} chunk(s), expected {_chunksPerPlane * _planeCount}.");
        }

        _chunkDecoder = new TiffChunkDecoder(directory);
        _planeChunkRowBytes = new int[_planeCount];
        _chunkBuffers = new byte[_chunksAcross * _planeCount][];

        for (int plane = 0; plane < _planeCount; plane++)
        {
            int chunkRowBits = _chunkWidth * _planeBitsPerPixel[plane];
            if (_chunksAcross > 1 && chunkRowBits % 8 != 0)
            {
                throw new InvalidDataException($"TIFF tile width {_chunkWidth} does not end on a byte boundary.");
            }

            int storedRowsPerChunk = _chunkHeight;
            _planeChunkRowBytes[plane] = (chunkRowBits + 7) / 8;

            if (_hasDataUnits)
            {
                int blocksAcross = (_chunkWidth + _subsamplingHorizontal - 1) / _subsamplingHorizontal;
                storedRowsPerChunk = (_chunkHeight + _subsamplingVertical - 1) / _subsamplingVertical;
                _planeChunkRowBytes[plane] = blocksAcross * ((_subsamplingHorizontal * _subsamplingVertical) + 2);
            }

            for (int column = 0; column < _chunksAcross; column++)
            {
                _chunkBuffers[(plane * _chunksAcross) + column] = new byte[_planeChunkRowBytes[plane] * storedRowsPerChunk];
            }
        }

        if (!isStoredAsOutput && !_hasDataUnits)
        {
            _storedRows = new byte[_planeCount][];
            for (int plane = 0; plane < _planeCount; plane++)
            {
                _storedRows[plane] = new byte[((Width * _planeBitsPerPixel[plane]) + 7) / 8];
            }
        }
    }

    /// <summary>
    /// Image width in pixels.
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Image height in pixels.
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Bit depth of every produced sample: the deepest stored sample, capped at 16.
    /// </summary>
    public int BitsPerComponent { get; }

    /// <summary>
    /// Number of samples per pixel, extra samples included.
    /// </summary>
    public int ComponentCount { get; }

    /// <summary>
    /// Byte length of each produced row.
    /// </summary>
    public int RowBytes { get; }

    /// <summary>
    /// Color model of the produced samples, or null when the directory declares none.
    /// </summary>
    public TiffPhotometric? Photometric { get; }

    /// <summary>
    /// Zero-based index of the next row to be produced.
    /// </summary>
    public int CurrentRow { get; private set; }

    /// <summary>
    /// Reads the next image row into <paramref name="destination"/>.
    /// </summary>
    /// <param name="destination">Row buffer, at least <see cref="RowBytes"/> long.</param>
    /// <returns>True when a row was written; false once every row has been produced.</returns>
    public bool TryReadRow(in Span<byte> destination)
    {
        if (CurrentRow >= Height)
        {
            return false;
        }

        if (destination.Length < RowBytes)
        {
            throw new ArgumentException($"Row buffer holds {destination.Length} bytes, expected {RowBytes}.", nameof(destination));
        }

        int chunkRow = CurrentRow / _chunkHeight;
        if (chunkRow != _loadedChunkRow)
        {
            LoadChunkRow(chunkRow);
        }

        int rowInChunk = CurrentRow - (chunkRow * _chunkHeight);

        if (_hasDataUnits && _yCbCrConverter != null)
        {
            ConvertDataUnitRow(_yCbCrConverter, rowInChunk, destination);
        }
        else if (_storedRows == null)
        {
            CopyChunkRow(0, rowInChunk, destination);
        }
        else
        {
            for (int plane = 0; plane < _planeCount; plane++)
            {
                CopyChunkRow(plane, rowInChunk, _storedRows[plane]);
            }

            NormalizeRow(_storedRows, destination.Slice(0, RowBytes));

            if (_yCbCrConverter != null)
            {
                ConvertRowInPlace(_yCbCrConverter, destination);
            }
        }

        CurrentRow++;
        return true;
    }

    private void LoadChunkRow(int chunkRow)
    {
        int rowsInChunk = _isTiled
            ? _chunkHeight
            : Math.Min(_chunkHeight, Height - (chunkRow * _chunkHeight));

        for (int plane = 0; plane < _planeCount; plane++)
        {
            for (int column = 0; column < _chunksAcross; column++)
            {
                int chunkIndex = (plane * _chunksPerPlane) + (chunkRow * _chunksAcross) + column;
                byte[] buffer = _chunkBuffers[(plane * _chunksAcross) + column];

                Array.Clear(buffer, 0, buffer.Length);

                if (_hasDataUnits)
                {
                    int dataUnitRows = (rowsInChunk + _subsamplingVertical - 1) / _subsamplingVertical;
                    _chunkDecoder.Decode(GetChunkData(chunkIndex), _planeChunkRowBytes[plane], dataUnitRows, DataUnitSampleBits, buffer);
                }
                else
                {
                    _chunkDecoder.Decode(GetChunkData(chunkIndex), _chunkWidth, rowsInChunk, _planeSampleBits[plane], buffer);
                }
            }
        }

        _loadedChunkRow = chunkRow;
    }

    private ReadOnlyMemory<byte> GetChunkData(int chunkIndex)
    {
        long offset = _chunkOffsets[chunkIndex];
        long byteCount = _chunkByteCounts[chunkIndex];

        if (offset < 0 || offset > _data.Length || byteCount < 0)
        {
            throw new InvalidDataException($"TIFF chunk {chunkIndex} at offset {offset} is outside the {_data.Length}-byte file.");
        }

        long available = Math.Min(byteCount, _data.Length - offset);
        return _data.Slice((int)offset, (int)available);
    }

    private void CopyChunkRow(int plane, int rowInChunk, in Span<byte> target)
    {
        int bitsPerPixel = _planeBitsPerPixel[plane];
        int chunkRowBytes = _planeChunkRowBytes[plane];

        for (int column = 0; column < _chunksAcross; column++)
        {
            int left = column * _chunkWidth;
            int pixelCount = Math.Min(_chunkWidth, Width - left);
            int byteCount = ((pixelCount * bitsPerPixel) + 7) / 8;

            byte[] buffer = _chunkBuffers[(plane * _chunksAcross) + column];
            buffer.AsSpan(rowInChunk * chunkRowBytes, byteCount).CopyTo(target.Slice(left * bitsPerPixel / 8));
        }
    }
}
