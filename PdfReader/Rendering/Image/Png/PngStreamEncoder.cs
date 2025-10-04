using System;
using System.IO;
using System.IO.Compression;

namespace PdfReader.Rendering.Image.Png
{
    /// <summary>
    /// Streaming PNG encoder for 8-bit RGBA images.
    /// Accepts rows in top-down order and writes a valid PNG without holding the full raster.
    ///
    /// Implementation notes:
    /// - Always encodes color type RGBA (6), bit depth 8, no interlace.
    /// - Provides adaptive per-row filter selection (None, Sub, Up, Average, Paeth) using a simple
    ///   sum-of-absolute-residuals heuristic.
    /// - Produces a single zlib stream split across one or more IDAT chunks. The zlib container requires a
    ///   two-byte header and a final Adler32 checksum. .NET DeflateStream emits raw DEFLATE blocks (RFC1951),
    ///   so we prepend a manual zlib header and append Adler32 ourselves (RFC1950 compliance).
    /// - IDAT chunk boundaries are arbitrary; compressed byte stream continuity is maintained.
    /// </summary>
    public sealed class PngStreamEncoder : IDisposable
    {
        private const int BytesPerPixel = 4;
        private const byte BitDepth = 8;
        private const byte ColorTypeRgba = 6;
        private const byte CompressionMethodDeflate = 0;
        private const byte FilterMethodAdaptive = 0; // PNG spec value (only one method defined currently)
        private const byte InterlaceNone = 0;
        private const int IdatFlushThreshold = 64 * 1024;

        // Filter type constants (PNG spec)
        private const byte FilterNone = 0;
        private const byte FilterSub = 1;
        private const byte FilterUp = 2;
        private const byte FilterAverage = 3;
        private const byte FilterPaeth = 4;

        private readonly Stream outputStream;
        private readonly int imageWidth;
        private readonly int imageHeight;

        private readonly byte[] rowWorkingBuffer; // [filter + filtered row bytes]
        private readonly byte[] previousRawRow;   // Unfiltered RGBA bytes of previous row
        private readonly MemoryStream idatBuffer; // Accumulated compressed bytes pending an IDAT chunk write.
        private readonly DeflateStream deflateStream; // Raw DEFLATE (no zlib header/trailer) targeting idatBuffer.

        private bool finished;
        private int currentRowIndex;
        private bool zlibHeaderWritten;
        private bool hasPreviousRow;

        // Adler32 state (RFC1950) for uncompressed data (including filter bytes).
        private uint adlerS1 = 1;
        private uint adlerS2;

        private static readonly uint[] CrcTable = CreateCrcTable();

        /// <summary>
        /// Creates a new streaming PNG encoder and writes the PNG signature plus IHDR chunk.
        /// </summary>
        /// <param name="output">Destination writable stream (not disposed by this encoder).</param>
        /// <param name="width">Image width in pixels.</param>
        /// <param name="height">Image height in pixels.</param>
        public PngStreamEncoder(Stream output, int width, int height)
        {
            if (output == null)
            {
                throw new ArgumentNullException(nameof(output));
            }

            if (width <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width));
            }

            if (height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(height));
            }

            outputStream = output;
            imageWidth = width;
            imageHeight = height;

            int stride = BytesPerPixel * imageWidth;
            rowWorkingBuffer = new byte[stride + 1];
            previousRawRow = new byte[stride]; // Zero-initialized for first row (acts as virtual 0 row)
            idatBuffer = new MemoryStream();
            deflateStream = new DeflateStream(idatBuffer, CompressionLevel.Optimal, leaveOpen: true);

            WriteSignature();
            WriteIHDR();
        }

        /// <summary>
        /// Writes a single top-down RGBA row. <paramref name="rgbaRow"/> length must equal width * 4.
        /// </summary>
        /// <param name="rgbaRow">Raw RGBA pixel data for the row (R,G,B,A per pixel).</param>
        public void WriteRow(ReadOnlySpan<byte> rgbaRow)
        {
            if (finished)
            {
                throw new InvalidOperationException("Cannot write rows after Finish was called.");
            }

            int stride = imageWidth * BytesPerPixel;
            if (rgbaRow.Length != stride)
            {
                throw new ArgumentException("Row length does not match expected pixel stride.", nameof(rgbaRow));
            }

            if (currentRowIndex >= imageHeight)
            {
                throw new InvalidOperationException("All image rows have already been written.");
            }

            if (!zlibHeaderWritten)
            {
                WriteZlibHeader();
                zlibHeaderWritten = true;
            }

            // Select best filter for this row.
            byte bestFilter = SelectBestFilter(rgbaRow, previousRawRow, hasPreviousRow, stride);

            // Emit filter byte.
            rowWorkingBuffer[0] = bestFilter;

            // Produce filtered bytes based on chosen filter.
            ApplyFilter(bestFilter, rgbaRow, previousRawRow, hasPreviousRow, stride, rowWorkingBuffer, 1);

            // Update Adler32 with (filter byte + filtered data).
            UpdateAdler32(rowWorkingBuffer, 0, stride + 1);

            // Compress filtered row.
            deflateStream.Write(rowWorkingBuffer, 0, stride + 1);

            // Preserve current raw row for next pass.
            rgbaRow.CopyTo(previousRawRow);
            hasPreviousRow = true;

            currentRowIndex++;

            if (idatBuffer.Length >= IdatFlushThreshold)
            {
                deflateStream.Flush();
                FlushIdatBuffer();
            }
        }

        /// <summary>
        /// Finalizes the PNG stream. Must be called after all rows have been written.
        /// </summary>
        public void Finish()
        {
            if (finished)
            {
                return;
            }

            if (currentRowIndex != imageHeight)
            {
                throw new InvalidOperationException("Finish called before all rows were written.");
            }

            deflateStream.Flush();
            deflateStream.Dispose();

            WriteAdler32();

            FlushIdatBuffer();

            WriteIEND();
            finished = true;
        }

        /// <summary>
        /// Disposes the encoder, attempting to finish if all rows were provided.
        /// </summary>
        public void Dispose()
        {
            if (!finished && currentRowIndex == imageHeight)
            {
                Finish();
            }
        }

        #region Filter Selection / Application

        private byte SelectBestFilter(ReadOnlySpan<byte> current, ReadOnlySpan<byte> previous, bool hasPrev, int stride)
        {
            // Evaluate all five filters. Early exit opportunities: if None cost is 0 (rare for natural images), it wins.
            long bestCost = long.MaxValue;
            byte best = FilterNone;

            EvaluateFilter(FilterNone, current, previous, hasPrev, stride, BytesPerPixel, ref bestCost, ref best);
            if (bestCost == 0)
            {
                return best;
            }

            EvaluateFilter(FilterSub, current, previous, hasPrev, stride, BytesPerPixel, ref bestCost, ref best);
            EvaluateFilter(FilterUp, current, previous, hasPrev, stride, BytesPerPixel, ref bestCost, ref best);
            EvaluateFilter(FilterAverage, current, previous, hasPrev, stride, BytesPerPixel, ref bestCost, ref best);
            EvaluateFilter(FilterPaeth, current, previous, hasPrev, stride, BytesPerPixel, ref bestCost, ref best);

            return best;
        }

        private static void EvaluateFilter(byte filterType, ReadOnlySpan<byte> current, ReadOnlySpan<byte> previous, bool hasPrev, int stride, int bpp, ref long bestCost, ref byte best)
        {
            long cost = 0;

            if (filterType == FilterNone)
            {
                for (int i = 0; i < stride; i++)
                {
                    cost += current[i]; // absolute residual of raw byte (residual = value - 0)
                    if (cost >= bestCost)
                    {
                        return;
                    }
                }
            }
            else if (filterType == FilterSub)
            {
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= bpp ? current[i - bpp] : 0;
                    int residual = current[i] - left;
                    cost += residual < 0 ? -residual : residual;
                    if (cost >= bestCost)
                    {
                        return;
                    }
                }
            }
            else if (filterType == FilterUp)
            {
                if (!hasPrev)
                {
                    // Up reduces to None for first row.
                    EvaluateFilter(FilterNone, current, previous, hasPrev, stride, bpp, ref bestCost, ref best);
                    return;
                }
                for (int i = 0; i < stride; i++)
                {
                    int residual = current[i] - previous[i];
                    cost += residual < 0 ? -residual : residual;
                    if (cost >= bestCost)
                    {
                        return;
                    }
                }
            }
            else if (filterType == FilterAverage)
            {
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= bpp ? current[i - bpp] : 0;
                    int up = hasPrev ? previous[i] : 0;
                    int predictor = (left + up) >> 1; // floor((left + up)/2)
                    int residual = current[i] - predictor;
                    cost += residual < 0 ? -residual : residual;
                    if (cost >= bestCost)
                    {
                        return;
                    }
                }
            }
            else if (filterType == FilterPaeth)
            {
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? current[i - bpp] : 0;          // left
                    int b = hasPrev ? previous[i] : 0;                // up
                    int c = (hasPrev && i >= bpp) ? previous[i - bpp] : 0; // up-left
                    int predictor = PaethPredictor(a, b, c);
                    int residual = current[i] - predictor;
                    cost += residual < 0 ? -residual : residual;
                    if (cost >= bestCost)
                    {
                        return;
                    }
                }
            }
            else
            {
                return;
            }

            if (cost < bestCost)
            {
                bestCost = cost;
                best = filterType;
            }
        }

        private static void ApplyFilter(byte filterType, ReadOnlySpan<byte> current, ReadOnlySpan<byte> previous, bool hasPrev, int stride, byte[] destination, int destOffset)
        {
            int bpp = BytesPerPixel;
            if (filterType == FilterNone)
            {
                for (int i = 0; i < stride; i++)
                {
                    destination[destOffset + i] = current[i];
                }
                return;
            }

            if (filterType == FilterSub)
            {
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= bpp ? current[i - bpp] : 0;
                    int val = current[i] - left;
                    destination[destOffset + i] = (byte)val;
                }
                return;
            }

            if (filterType == FilterUp)
            {
                if (!hasPrev)
                {
                    // Same as None on first row.
                    ApplyFilter(FilterNone, current, previous, hasPrev, stride, destination, destOffset);
                    return;
                }
                for (int i = 0; i < stride; i++)
                {
                    int val = current[i] - previous[i];
                    destination[destOffset + i] = (byte)val;
                }
                return;
            }

            if (filterType == FilterAverage)
            {
                for (int i = 0; i < stride; i++)
                {
                    int left = i >= bpp ? current[i - bpp] : 0;
                    int up = hasPrev ? previous[i] : 0;
                    int predictor = (left + up) >> 1;
                    int val = current[i] - predictor;
                    destination[destOffset + i] = (byte)val;
                }
                return;
            }

            if (filterType == FilterPaeth)
            {
                for (int i = 0; i < stride; i++)
                {
                    int a = i >= bpp ? current[i - bpp] : 0;
                    int b = hasPrev ? previous[i] : 0;
                    int c = (hasPrev && i >= bpp) ? previous[i - bpp] : 0;
                    int predictor = PaethPredictor(a, b, c);
                    int val = current[i] - predictor;
                    destination[destOffset + i] = (byte)val;
                }
                return;
            }
        }

        private static int PaethPredictor(int a, int b, int c)
        {
            int p = a + b - c;
            int pa = p - a;
            if (pa < 0)
            {
                pa = -pa;
            }
            int pb = p - b;
            if (pb < 0)
            {
                pb = -pb;
            }
            int pc = p - c;
            if (pc < 0)
            {
                pc = -pc;
            }
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

        #endregion

        #region Zlib header / Adler32

        private void WriteZlibHeader()
        {
            idatBuffer.WriteByte(0x78);
            idatBuffer.WriteByte(0x9C);
        }

        private void UpdateAdler32(byte[] buffer, int offset, int count)
        {
            const uint Mod = 65521;
            uint s1 = adlerS1;
            uint s2 = adlerS2;
            for (int i = 0; i < count; i++)
            {
                s1 += buffer[offset + i];
                if (s1 >= Mod)
                {
                    s1 -= Mod;
                }
                s2 += s1;
                if (s2 >= Mod)
                {
                    s2 -= Mod;
                }
            }
            adlerS1 = s1;
            adlerS2 = s2;
        }

        private void WriteAdler32()
        {
            uint adler = (adlerS2 << 16) | adlerS1;
            WriteUInt32BigEndian(idatBuffer, adler);
        }

        #endregion

        #region PNG structural writers

        private void WriteSignature()
        {
            outputStream.WriteByte(0x89);
            outputStream.WriteByte((byte)'P');
            outputStream.WriteByte((byte)'N');
            outputStream.WriteByte((byte)'G');
            outputStream.WriteByte(0x0D);
            outputStream.WriteByte(0x0A);
            outputStream.WriteByte(0x1A);
            outputStream.WriteByte(0x0A);
        }

        private void WriteIHDR()
        {
            byte[] ihdr = new byte[13];
            WriteInt32BigEndian(ihdr, 0, imageWidth);
            WriteInt32BigEndian(ihdr, 4, imageHeight);
            ihdr[8] = BitDepth;
            ihdr[9] = ColorTypeRgba;
            ihdr[10] = CompressionMethodDeflate;
            ihdr[11] = FilterMethodAdaptive;
            ihdr[12] = InterlaceNone;
            WriteChunk("IHDR", ihdr, 0, ihdr.Length);
        }

        private void WriteIEND()
        {
            WriteChunk("IEND", Array.Empty<byte>(), 0, 0);
        }

        private void WriteChunk(string type, byte[] data, int offset, int length)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (type.Length != 4)
            {
                throw new ArgumentException("PNG chunk type must be 4 characters.", nameof(type));
            }

            WriteInt32BigEndian(outputStream, length);
            byte[] typeBytes = new byte[4]
            {
                (byte)type[0],
                (byte)type[1],
                (byte)type[2],
                (byte)type[3]
            };
            outputStream.Write(typeBytes, 0, 4);
            if (length > 0)
            {
                outputStream.Write(data, offset, length);
            }
            uint crc = UpdateCrc(0xFFFFFFFFu, typeBytes, 0, 4);
            if (length > 0)
            {
                crc = UpdateCrc(crc, data, offset, length);
            }
            crc ^= 0xFFFFFFFFu;
            WriteUInt32BigEndian(outputStream, crc);
        }

        private void FlushIdatBuffer()
        {
            if (idatBuffer.Length == 0)
            {
                return;
            }

            byte[] buffer = idatBuffer.ToArray();
            WriteChunk("IDAT", buffer, 0, buffer.Length);
            idatBuffer.SetLength(0);
        }

        #endregion

        #region CRC32

        private static uint[] CreateCrcTable()
        {
            var table = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                    {
                        c = 0xEDB88320u ^ (c >> 1);
                    }
                    else
                    {
                        c >>= 1;
                    }
                }
                table[n] = c;
            }
            return table;
        }

        private static uint UpdateCrc(uint crc, byte[] data, int offset, int count)
        {
            uint c = crc;
            for (int i = 0; i < count; i++)
            {
                c = CrcTable[(c ^ data[offset + i]) & 0xFF] ^ (c >> 8);
            }
            return c;
        }

        #endregion

        #region Binary helpers

        private static void WriteInt32BigEndian(Stream stream, int value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteUInt32BigEndian(Stream stream, uint value)
        {
            stream.WriteByte((byte)((value >> 24) & 0xFF));
            stream.WriteByte((byte)((value >> 16) & 0xFF));
            stream.WriteByte((byte)((value >> 8) & 0xFF));
            stream.WriteByte((byte)(value & 0xFF));
        }

        private static void WriteInt32BigEndian(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)((value >> 24) & 0xFF);
            buffer[offset + 1] = (byte)((value >> 16) & 0xFF);
            buffer[offset + 2] = (byte)((value >> 8) & 0xFF);
            buffer[offset + 3] = (byte)(value & 0xFF);
        }

        #endregion
    }
}
