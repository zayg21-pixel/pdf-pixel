using System;
using System.IO;

namespace PdfReader.Streams
{
    /// <summary>
    /// Stream wrapper that applies TIFF (2) and PNG (10..15) predictors to an already filter-decoded data buffer.
    /// Supports 1,2,4,8 and 16 bits per component (byte-aligned output for 8/16; packed for 1/2/4).
    /// For PNG predictors with sub-byte sample sizes, filtering occurs on packed bytes as per PNG specification.
    /// For TIFF predictor with sub-byte sample sizes, individual samples are reconstructed bit-wise (modulo sample range) and re-packed.
    /// </summary>
    internal sealed class PredictorDecodeStream : Stream
    {
        private readonly byte[] _buffer;
        private int _position;

        public PredictorDecodeStream(Stream decoded, int predictor, int colors, int bitsPerComponent, int columns)
        {
            if (decoded == null)
            {
                throw new System.ArgumentNullException(nameof(decoded));
            }
            if (colors <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(colors));
            }
            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8 && bitsPerComponent != 16)
            {
                throw new System.NotSupportedException("Only 1,2,4,8 or 16 bits per component predictors are supported.");
            }
            if (columns <= 0)
            {
                throw new System.ArgumentOutOfRangeException(nameof(columns));
            }
            using (var ms = new MemoryStream())
            {
                decoded.CopyTo(ms);
                var raw = ms.ToArray();
                _buffer = ApplyPredictor(raw, predictor, colors, bitsPerComponent, columns);
            }
            _position = 0;
        }

        public override bool CanRead { get { return true; } }
        public override bool CanSeek { get { return true; } }
        public override bool CanWrite { get { return false; } }
        public override long Length { get { return _buffer.Length; } }

        public override long Position
        {
            get { return _position; }
            set
            {
                if (value < 0 || value > _buffer.Length)
                {
                    throw new System.ArgumentOutOfRangeException(nameof(value));
                }
                _position = (int)value;
            }
        }

        public override void Flush() { }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null)
            {
                throw new System.ArgumentNullException(nameof(buffer));
            }
            if (offset < 0 || count < 0 || offset + count > buffer.Length)
            {
                throw new System.ArgumentOutOfRangeException();
            }
            int remaining = _buffer.Length - _position;
            if (remaining <= 0)
            {
                return 0;
            }
            int toCopy = System.Math.Min(count, remaining);
            System.Array.Copy(_buffer, _position, buffer, offset, toCopy);
            _position += toCopy;
            return toCopy;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            int newPos;
            switch (origin)
            {
                case SeekOrigin.Begin:
                    newPos = (int)offset;
                    break;
                case SeekOrigin.Current:
                    newPos = _position + (int)offset;
                    break;
                case SeekOrigin.End:
                    newPos = _buffer.Length + (int)offset;
                    break;
                default:
                    throw new System.ArgumentOutOfRangeException(nameof(origin));
            }
            if (newPos < 0 || newPos > _buffer.Length)
            {
                throw new IOException("Attempted to seek outside stream bounds.");
            }
            _position = newPos;
            return _position;
        }

        public override void SetLength(long value)
        {
            throw new System.NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new System.NotSupportedException();
        }

        private static byte[] ApplyPredictor(byte[] data, int predictor, int colors, int bitsPerComponent, int columns)
        {
            int bytesPerSample = bitsPerComponent >= 8 ? (bitsPerComponent + 7) / 8 : 1;
            int decodedRowBytes = bitsPerComponent >= 8
                ? columns * colors * bytesPerSample
                : (columns * colors * bitsPerComponent + 7) / 8; // packed

            if (predictor == 2)
            {
                if (decodedRowBytes <= 0 || data.Length % decodedRowBytes != 0)
                {
                    return data;
                }
                byte[] output = new byte[data.Length];
                int totalRows = data.Length / decodedRowBytes;
                if (bitsPerComponent >= 8)
                {
                    // Existing 8/16-bit path
                    int samplesPerRow = columns * colors;
                    for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
                    {
                        int rowOffset = rowIndex * decodedRowBytes;
                        if (bytesPerSample == 1)
                        {
                            for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                            {
                                int leftIndex = sampleIndex - colors;
                                int left = leftIndex >= 0 ? output[rowOffset + leftIndex] : 0;
                                output[rowOffset + sampleIndex] = (byte)((data[rowOffset + sampleIndex] + left) & 0xFF);
                            }
                        }
                        else
                        {
                            for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                            {
                                int byteIndex = rowOffset + sampleIndex * bytesPerSample;
                                int current = (data[byteIndex] << 8) | data[byteIndex + 1];
                                int left = 0;
                                if (sampleIndex >= colors)
                                {
                                    int leftByteIndex = rowOffset + (sampleIndex - colors) * bytesPerSample;
                                    left = (output[leftByteIndex] << 8) | output[leftByteIndex + 1];
                                }
                                int decoded = (current + left) & 0xFFFF;
                                output[byteIndex] = (byte)(decoded >> 8);
                                output[byteIndex + 1] = (byte)(decoded & 0xFF);
                            }
                        }
                    }
                }
                else
                {
                    // Sub-byte TIFF predictor: operate per sample with modulo arithmetic then re-pack.
                    int bits = bitsPerComponent;
                    int samplesPerRow = columns * colors;
                    int sampleMask = (1 << bits) - 1;
                    for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
                    {
                        int rowOffset = rowIndex * decodedRowBytes;
                        // Read and decode each sample.
                        int bitPos = 0; // bit position within row
                        int prevValuesCount = samplesPerRow; // for clarity
                        int[] decodedSamples = new int[samplesPerRow];
                        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                        {
                            int byteIndex = rowOffset + (bitPos >> 3);
                            int intraByteBit = bitPos & 7;
                            int remainingBitsInByte = 8 - intraByteBit;
                            int value;
                            if (remainingBitsInByte >= bits)
                            {
                                int shift = remainingBitsInByte - bits;
                                value = (data[byteIndex] >> shift) & sampleMask;
                            }
                            else
                            {
                                // Value spans bytes.
                                int firstPart = data[byteIndex] & ((1 << remainingBitsInByte) - 1);
                                int secondPart = data[byteIndex + 1] >> (8 - (bits - remainingBitsInByte));
                                value = ((firstPart << (bits - remainingBitsInByte)) | secondPart) & sampleMask;
                            }
                            int leftIndex = sampleIndex - colors;
                            int left = leftIndex >= 0 ? decodedSamples[leftIndex] : 0;
                            int decodedValue = (value + left) & sampleMask;
                            decodedSamples[sampleIndex] = decodedValue;
                            bitPos += bits;
                        }
                        // Re-pack decoded samples.
                        int outBitPos = 0;
                        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                        {
                            int value = decodedSamples[sampleIndex] & sampleMask;
                            int outByteIndex = rowOffset + (outBitPos >> 3);
                            int outIntra = outBitPos & 7;
                            int freeBits = 8 - outIntra;
                            if (freeBits >= bits)
                            {
                                int shift = freeBits - bits;
                                output[outByteIndex] &= (byte)~(((sampleMask) << shift) & 0xFF);
                                output[outByteIndex] |= (byte)((value & sampleMask) << shift);
                            }
                            else
                            {
                                int firstPartBits = freeBits;
                                int secondPartBits = bits - freeBits;
                                int firstMask = (1 << firstPartBits) - 1;
                                int firstValue = (value >> secondPartBits) & firstMask;
                                output[outByteIndex] &= (byte)~firstMask;
                                output[outByteIndex] |= (byte)firstValue;
                                int secondValue = value & ((1 << secondPartBits) - 1);
                                int secondShift = 8 - secondPartBits;
                                output[outByteIndex + 1] &= (byte)~(((1 << secondPartBits) - 1) << secondShift);
                                output[outByteIndex + 1] |= (byte)(secondValue << secondShift);
                            }
                            outBitPos += bits;
                        }
                    }
                }
                return output;
            }

            if (predictor >= 10 && predictor <= 15)
            {
                int encodedRowBytes = decodedRowBytes + 1;
                if (encodedRowBytes <= 1 || data.Length % encodedRowBytes != 0)
                {
                    return data;
                }
                int totalRows = data.Length / encodedRowBytes;
                byte[] output = new byte[totalRows * decodedRowBytes];
                byte[] prevRow = new byte[decodedRowBytes];
                int bytesPerPixel = (colors * bitsPerComponent + 7) / 8; // PNG spec definition
                for (int rowIndex = 0; rowIndex < totalRows; rowIndex++)
                {
                    int encodedOffset = rowIndex * encodedRowBytes;
                    byte filter = data[encodedOffset];
                    int outOffset = rowIndex * decodedRowBytes;
                    for (int i = 0; i < decodedRowBytes; i++)
                    {
                        int raw = data[encodedOffset + 1 + i];
                        int left = i >= bytesPerPixel ? output[outOffset + i - bytesPerPixel] : 0;
                        int up = rowIndex > 0 ? prevRow[i] : 0;
                        int upLeft = (rowIndex > 0 && i >= bytesPerPixel) ? prevRow[i - bytesPerPixel] : 0;
                        int decodedByte = filter switch
                        {
                            0 => raw,
                            1 => raw + left,
                            2 => raw + up,
                            3 => raw + ((left + up) >> 1),
                            4 => raw + Paeth(left, up, upLeft),
                            _ => raw
                        };
                        output[outOffset + i] = (byte)decodedByte;
                    }
                    System.Array.Copy(output, outOffset, prevRow, 0, decodedRowBytes);
                }
                return output;
            }

            return data;
        }

        private static int Paeth(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = System.Math.Abs(p - a);
            int pb = System.Math.Abs(p - b);
            int pc = System.Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
            {
                return a;
            }
            if (pb <= pc)
            {
                return b;
            }
            return c;
        }
    }
}
