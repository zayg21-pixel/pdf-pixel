using PdfPixel.Color.Structures;
using SkiaSharp;
using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace PdfPixel.Imaging.Png
{
    /// <summary>
    /// State machine for building PNG images.
    /// </summary>
    public sealed class PngImageBuilder : IDisposable
    {
        private enum PngImageBuilderState
        {
            Init,
            BuildImage,
            ImageDataWritten,
            Completed
        }

        private readonly int _channels;
        private readonly int _bitsPerComponent;
        private readonly int _width;
        private readonly int _height;
        private readonly int _encodedRowLength;
        private readonly int _rowWithFilter;
        private readonly int _numBlocks;
        private readonly int _totalUncompressed;
        private readonly int _totalData;

        private readonly SKDynamicMemoryWStream _pngStream = new SKDynamicMemoryWStream();
        private PngImageBuilderState _state = PngImageBuilderState.Init;


        // Streaming PNG row writing state
        private bool _rowStreamingActive;
        private int _rowStreamingRowsWritten;
        private System.IO.Hashing.Crc32 _rowStreamingCrc32;
        private byte[] _rowStreamingBlockHeader;
        private const int MaxDeflateBlockSize = 65535;
        private int _rowStreamingBlockBytesRemaining;
        private int _rowStreamingUncompressedBytesLeft; // Track uncompressed bytes left
        private byte[] _rowBuffer;

        public PngImageBuilder(int channels, int bitsPerComponent, int width, int height)
        {
            if (channels < 1 || channels > 4)
            {
                throw new ArgumentOutOfRangeException(nameof(channels), "Channels must be between 1 and 4.");
            }

            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 &&
                bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                throw new ArgumentOutOfRangeException(nameof(bitsPerComponent), "Bits per component must be 1, 2, 4, 8, or 16.");
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than 0.");
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than 0.");
            }

            _channels = channels;
            _bitsPerComponent = bitsPerComponent;
            _width = width;
            _height = height;
            _encodedRowLength = (width * channels * bitsPerComponent + 7) / 8;

            _rowWithFilter = 1 + _encodedRowLength;
            _totalUncompressed = _height * _rowWithFilter;
            _numBlocks = (_totalUncompressed + MaxDeflateBlockSize - 1) / MaxDeflateBlockSize;
            _totalData = 2 + _totalUncompressed + _numBlocks * 5;
        }

        /// <summary>
        /// Initializes the builder with palette and ICC profile, writes all initial data, and transitions to BuildImage state.
        /// </summary>
        /// <param name="palette">The color palette to use (can be null).</param>
        /// <param name="iccProfile">The ICC profile to use (can be empty).</param>
        public void Init(RgbaPacked[] palette, ReadOnlyMemory<byte> iccProfile)
        {
            EnsureState(PngImageBuilderState.Init);
            PngHelpers.WritePngSignature(_pngStream);

            byte colorType;
            if (_channels == 1)
            {
                if (palette != null && palette.Length > 0)
                {
                    colorType = 3; // Indexed-color
                }
                else
                {
                    colorType = 0; // Grayscale
                }
            }
            else if (_channels == 3)
            {
                colorType = 2; // Truecolor
            }
            else if (_channels == 4)
            {
                colorType = 6; // Truecolor with alpha
            }
            else
            {
                throw new NotSupportedException($"Unsupported number of channels: {_channels}");
            }

            PngHelpers.WriteIhdrChunk(_pngStream, _width, _height, (byte)_bitsPerComponent, colorType);

            if (_channels == 1 && palette != null && palette.Length > 0)
            {
                PngHelpers.WritePlteChunk(_pngStream, palette);
            }

            if (!iccProfile.IsEmpty)
            {
                PngHelpers.WriteIccpChunk(_pngStream, iccProfile);
            }

            _rowBuffer = new byte[_encodedRowLength];
            _state = PngImageBuilderState.BuildImage;
        }

        public void SetPngImageBytes(byte[] data)
        {
            EnsureState(PngImageBuilderState.BuildImage);
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }
            PngHelpers.WriteChunk(_pngStream, "IDAT", data, data.Length);
            _state = PngImageBuilderState.ImageDataWritten;
        }

        public void SetPngImageBytes(Stream dataStream)
        {
            EnsureState(PngImageBuilderState.BuildImage);
            if (dataStream == null)
            {
                throw new ArgumentNullException(nameof(dataStream));
            }
            using var ms = new MemoryStream();
            dataStream.CopyTo(ms);
            PngHelpers.WriteChunk(_pngStream, "IDAT", ms.ToArray(), (int)ms.Length);
            _state = PngImageBuilderState.ImageDataWritten;
        }

        /// <summary>
        /// Writes a single PNG image row. Prepends filter byte 0, handles zlib/DEFLATE block splitting, chunk headers, and finalization automatically.
        /// </summary>
        /// <param name="row">The row data (without filter byte).</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void WritePngImageRow(ReadOnlySpan<byte> row)
        {
            if (row.IsEmpty)
            {
                throw new ArgumentNullException(nameof(row));
            }
            if (_state != PngImageBuilderState.BuildImage)
            {
                throw new InvalidOperationException("WritePngImageRow can only be called after Init and before Build.");
            }

            if (!_rowStreamingActive)
            {
                _rowStreamingActive = true;
                _rowStreamingRowsWritten = 0;
                _rowStreamingCrc32 = new System.IO.Hashing.Crc32();
                _rowStreamingBlockHeader = new byte[5];
                _rowStreamingBlockBytesRemaining = 0;
                _rowStreamingUncompressedBytesLeft = _totalUncompressed;
                PngHelpers.WriteChunkHeader(_pngStream, "IDAT", _totalData, _rowStreamingCrc32);
                PngHelpers.WriteZLibSignature(_pngStream, _rowStreamingCrc32);
            }

            int rowDataOffset = 0;
            int rowDataRemaining = row.Length;
            bool filterByteWritten = false;

            while (rowDataRemaining > 0 || !filterByteWritten)
            {
                if (_rowStreamingBlockBytesRemaining == 0)
                {
                    if (_rowStreamingUncompressedBytesLeft <= 0)
                    {
                        throw new InvalidOperationException("No uncompressed bytes left to write, but more data was provided.");
                    }
                    int blockSize = Math.Min(MaxDeflateBlockSize, _rowStreamingUncompressedBytesLeft);
                    bool isFinalBlock = (blockSize == _rowStreamingUncompressedBytesLeft);
                    PngHelpers.UpdateDeflateBlockHeader(_rowStreamingBlockHeader, blockSize, isFinalBlock);
                    PngHelpers.WriteChunkData(_pngStream, _rowStreamingBlockHeader, 5, _rowStreamingCrc32);
                    _rowStreamingBlockBytesRemaining = blockSize;
                }

                // Write filter byte if not yet written
                if (!filterByteWritten)
                {
                    PngHelpers.WriteChunkData(_pngStream, [0], 1, _rowStreamingCrc32);
                    _rowStreamingBlockBytesRemaining--;
                    _rowStreamingUncompressedBytesLeft--;
                    filterByteWritten = true;
                    continue;
                }

                // Write as much row data as fits in the current block
                int toWrite = Math.Min(_rowStreamingBlockBytesRemaining, rowDataRemaining);
                if (toWrite > 0)
                {
                    row.Slice(rowDataOffset, toWrite).CopyTo(_rowBuffer);
                    PngHelpers.WriteChunkData(_pngStream, _rowBuffer, toWrite, _rowStreamingCrc32);
                    _rowStreamingBlockBytesRemaining -= toWrite;
                    _rowStreamingUncompressedBytesLeft -= toWrite;
                    rowDataOffset += toWrite;
                    rowDataRemaining -= toWrite;
                }
            }

            _rowStreamingRowsWritten++;
            if (_rowStreamingRowsWritten == _height)
            {
                PngHelpers.CompleteChunk(_pngStream, _rowStreamingCrc32);
                _state = PngImageBuilderState.ImageDataWritten;
            }
        }

        /// <summary>
        /// Builds the PNG image. Can only be called once per instance.
        /// /// <summary>
        /// Builds the PNG image. Can only be called once per instance.
        /// </summary>
        /// <returns>The built SKImage.</returns>
        public SKImage Build()
        {
            if (_state != PngImageBuilderState.ImageDataWritten)
            {
                EnsureState(PngImageBuilderState.ImageDataWritten);
            }
            PngHelpers.WriteChunk(_pngStream, "IEND", Array.Empty<byte>(), 0);
            _pngStream.Flush();
            _state = PngImageBuilderState.Completed;
            using var data = _pngStream.DetachAsData();
            var result = SKImage.FromEncodedData(data);
            return result;
        }

        private void EnsureState(PngImageBuilderState requiredState)
        {
            if (_state != requiredState)
            {
                throw new InvalidOperationException($"Operation not allowed in state: {_state}. Required state: {requiredState}.");
            }
        }

        public void Dispose()
        {
            _pngStream.Dispose();
        }
    }
}
