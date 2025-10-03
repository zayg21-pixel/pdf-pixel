using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace PdfReader.Rendering.Image.Raw
{
    internal static class PdfRawImageUtilities
    {
        /// <summary>
        /// Decode raw PDF image data: applies predictor correction only, preserving original bit depth.
        /// Resampling to 8-bit is now handled by PdfImagePostProcessor after /Decode application.
        /// </summary>
        /// <param name="image">PDF image with metadata and decode parameters.</param>
        /// <param name="rawData">Raw image data after filter decompression.</param>
        /// <returns>IntPtr to allocated predictor-corrected buffer at original bit depth, or IntPtr.Zero on failure. Caller must free with Marshal.FreeHGlobal.</returns>
        public static unsafe IntPtr Decode(PdfImage image, ReadOnlyMemory<byte> rawData)
        {
            if (image == null || image.Width <= 0 || image.Height <= 0)
            {
                return IntPtr.Zero;
            }

            var converter = image.ColorSpaceConverter;
            if (converter == null)
            {
                return IntPtr.Zero;
            }

            int components = converter.Components;
            int bitsPerComponent = image.BitsPerComponent;
            
            // Check if predictor correction is needed
            bool needsPredictor = image.DecodeParms != null && image.DecodeParms.Count > 0;
            
            if (!needsPredictor)
            {
                // Fast path: just copy the data at original bit depth
                return CopyRawData(rawData);
            }

            // Apply predictor correction while preserving original bit depth
            var decodeParameters = image.DecodeParms[0];
            int predictor = decodeParameters.Predictor ?? 1;
            
            if (predictor == 1)
            {
                // No predictor - fall back to simple copy
                return CopyRawData(rawData);
            }

            int colors = decodeParameters.Colors ?? components;
            int decodeBitsPerComponent = decodeParameters.BitsPerComponent ?? bitsPerComponent;
            int columns = decodeParameters.Columns ?? image.Width;

            if (columns <= 0 || colors <= 0 || decodeBitsPerComponent <= 0)
            {
                return IntPtr.Zero;
            }

            try
            {
                if (predictor == 2)
                {
                    return DecodeTiffPredictorOnly(image, rawData, colors, decodeBitsPerComponent, columns);
                }
                else if (predictor >= 10 && predictor <= 15)
                {
                    return DecodePngPredictorOnly(image, rawData, colors, decodeBitsPerComponent, columns);
                }
                else
                {
                    // Unknown predictor - fall back to simple copy
                    return CopyRawData(rawData);
                }
            }
            catch
            {
                // Fall back to simple copy on error
                return CopyRawData(rawData);
            }
        }

        /// <summary>
        /// Copy raw data to allocated buffer without any processing.
        /// </summary>
        private static unsafe IntPtr CopyRawData(ReadOnlyMemory<byte> rawData)
        {
            if (rawData.Length == 0)
            {
                return IntPtr.Zero;
            }

            IntPtr buffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(rawData.Length);
            try
            {
                using (var handle = rawData.Pin())
                {
                    Buffer.MemoryCopy(handle.Pointer, (void*)buffer, rawData.Length, rawData.Length);
                }
                return buffer;
            }
            catch
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(buffer);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// TIFF predictor correction only, preserving original bit depth.
        /// </summary>
        private static unsafe IntPtr DecodeTiffPredictorOnly(PdfImage image, ReadOnlyMemory<byte> rawData, 
            int colors, int decodeBitsPerComponent, int columns)
        {
            int samplesPerRow = columns * colors;
            int height = image.Height;
            
            // Calculate source stride (predictor-encoded data)
            int sourceRowStride;
            if (decodeBitsPerComponent >= 8)
            {
                int bytesPerSample = (decodeBitsPerComponent + 7) / 8;
                sourceRowStride = samplesPerRow * bytesPerSample;
            }
            else
            {
                sourceRowStride = (samplesPerRow * decodeBitsPerComponent + 7) / 8;
            }

            if (sourceRowStride * height > rawData.Length)
            {
                return IntPtr.Zero;
            }

            // Allocate output buffer same size as input (predictor correction doesn't change size)
            var outputBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(rawData.Length);
            
            try
            {
                using var handle = rawData.Pin();
                byte* source = (byte*)handle.Pointer;
                byte* output = (byte*)outputBuffer.ToPointer();

                // Copy input to output first
                System.Runtime.InteropServices.Marshal.Copy(rawData.ToArray(), 0, outputBuffer, rawData.Length);

                if (decodeBitsPerComponent >= 8)
                {
                    // >= 8 bpc: decode differences in place
                    Parallel.For(0, height, rowIndex =>
                    {
                        int rowStart = rowIndex * sourceRowStride;
                        byte* row = output + rowStart;

                        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                        {
                            if (decodeBitsPerComponent == 16)
                            {
                                // 16-bit: decode difference
                                int byteIndex = sampleIndex * 2;
                                if (byteIndex + 1 < sourceRowStride)
                                {
                                    int currentHi = row[byteIndex];
                                    int currentLo = row[byteIndex + 1];
                                    int current16 = (currentHi << 8) | currentLo;
                                    
                                    int leftSample = 0;
                                    if (sampleIndex >= colors)
                                    {
                                        int leftByteIndex = (sampleIndex - colors) * 2;
                                        int leftHi = row[leftByteIndex];
                                        int leftLo = row[leftByteIndex + 1];
                                        leftSample = (leftHi << 8) | leftLo;
                                    }
                                    
                                    int decoded16 = (current16 + leftSample) & 0xFFFF;
                                    row[byteIndex] = (byte)(decoded16 >> 8);
                                    row[byteIndex + 1] = (byte)(decoded16 & 0xFF);
                                }
                            }
                            else
                            {
                                // 8-bit: decode difference
                                int byteIndex = sampleIndex;
                                if (byteIndex < sourceRowStride)
                                {
                                    int current = row[byteIndex];
                                    int left = sampleIndex >= colors ? row[byteIndex - colors] : 0;
                                    row[byteIndex] = (byte)((current + left) & 0xFF);
                                }
                            }
                        }
                    });
                }
                else
                {
                    // Sub-8 bpc: decode packed samples in place (more complex, preserve packing)
                    int mask = (1 << decodeBitsPerComponent) - 1;
                    
                    for (int rowIndex = 0; rowIndex < height; rowIndex++)
                    {
                        int rowStart = rowIndex * sourceRowStride;
                        byte* row = output + rowStart;
                        
                        // Decode samples within packed bytes
                        int prevSample = 0;
                        int bitIndex = 0;
                        
                        for (int sampleIndex = 0; sampleIndex < samplesPerRow; sampleIndex++)
                        {
                            int currentSample = ReadPackedSample(row, 0, ref bitIndex, decodeBitsPerComponent);
                            int decodedSample = (currentSample + prevSample) & mask;
                            
                            // Write back decoded sample (complex bit manipulation needed)
                            WritePackedSample(row, 0, sampleIndex, decodedSample, decodeBitsPerComponent);
                            prevSample = decodedSample;
                        }
                    }
                }

                return outputBuffer;
            }
            catch
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(outputBuffer);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// PNG predictor correction only, preserving original bit depth.
        /// </summary>
        private static unsafe IntPtr DecodePngPredictorOnly(PdfImage image, ReadOnlyMemory<byte> rawData,
            int colors, int decodeBitsPerComponent, int columns)
        {
            int bytesPerPixel = (colors * decodeBitsPerComponent + 7) / 8;
            int rowLength = bytesPerPixel * columns;
            int encodedRowLength = rowLength + 1; // +1 for filter byte
            int height = image.Height;
            
            if (bytesPerPixel <= 0 || rowLength <= 0 || rawData.Length < encodedRowLength * height)
            {
                return IntPtr.Zero;
            }

            // Allocate output buffer same size as input
            var outputBuffer = System.Runtime.InteropServices.Marshal.AllocHGlobal(rawData.Length);
            
            try
            {
                using var handle = rawData.Pin();
                byte* source = (byte*)handle.Pointer;
                byte* output = (byte*)outputBuffer.ToPointer();

                // Copy input to output first
                System.Runtime.InteropServices.Marshal.Copy(rawData.ToArray(), 0, outputBuffer, rawData.Length);

                // PNG predictor correction (sequential due to dependencies)
                for (int rowIndex = 0; rowIndex < height; rowIndex++)
                {
                    int sourceRowStart = rowIndex * encodedRowLength;
                    byte filter = output[sourceRowStart];

                    for (int byteIndex = 0; byteIndex < rowLength; byteIndex++)
                    {
                        int sourceIndex = sourceRowStart + 1 + byteIndex;
                        int raw = output[sourceIndex];
                        
                        // Apply PNG filter
                        int left = byteIndex >= bytesPerPixel ? output[sourceRowStart + 1 + byteIndex - bytesPerPixel] : 0;
                        int up = rowIndex > 0 ? output[(rowIndex - 1) * encodedRowLength + 1 + byteIndex] : 0;
                        int upLeft = (rowIndex > 0 && byteIndex >= bytesPerPixel) ? 
                            output[(rowIndex - 1) * encodedRowLength + 1 + byteIndex - bytesPerPixel] : 0;

                        int decoded;
                        switch (filter)
                        {
                            case 0: decoded = raw; break; // None
                            case 1: decoded = raw + left; break; // Sub
                            case 2: decoded = raw + up; break; // Up
                            case 3: decoded = raw + ((left + up) >> 1); break; // Average
                            case 4: decoded = raw + PaethPredictor(left, up, upLeft); break; // Paeth
                            default: decoded = raw; break; // Unknown - use raw
                        }

                        output[sourceIndex] = (byte)decoded;
                    }
                }

                return outputBuffer;
            }
            catch
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(outputBuffer);
                return IntPtr.Zero;
            }
        }

        /// <summary>
        /// Read a packed sample from source data - used only for predictor correction.
        /// </summary>
        private static unsafe int ReadPackedSample(byte* source, int rowStartOffset, ref int bitIndex, int bitsPerComponent)
        {
            int sample;
            int byteIndex = rowStartOffset + (bitIndex >> 3);

            switch (bitsPerComponent)
            {
                case 1:
                    {
                        int bitPosition = 7 - (bitIndex & 7);
                        sample = (source[byteIndex] >> bitPosition) & 0x1;
                        bitIndex += 1;
                        return sample; // Return raw value for predictor processing
                    }
                case 2:
                    {
                        int shift = 6 - (bitIndex & 6);
                        sample = (source[byteIndex] >> shift) & 0x3;
                        bitIndex += 2;
                        return sample;
                    }
                case 4:
                    {
                        bool isHighNibble = (bitIndex & 4) == 0;
                        sample = isHighNibble ? ((source[byteIndex] >> 4) & 0xF) : (source[byteIndex] & 0xF);
                        bitIndex += 4;
                        return sample;
                    }
                case 8:
                    {
                        sample = source[byteIndex];
                        bitIndex += 8;
                        return sample;
                    }
                default:
                    return 0;
            }
        }

        /// <summary>
        /// Write a packed sample back to source data.
        /// </summary>
        private static unsafe void WritePackedSample(byte* data, int rowStartOffset, int sampleIndex, int sampleValue, int bitsPerComponent)
        {
            int bitIndex = sampleIndex * bitsPerComponent;
            int byteIndex = rowStartOffset + (bitIndex >> 3);
            
            switch (bitsPerComponent)
            {
                case 1:
                    {
                        int bitPosition = 7 - (bitIndex & 7);
                        int mask = ~(1 << bitPosition);
                        data[byteIndex] = (byte)((data[byteIndex] & mask) | ((sampleValue & 1) << bitPosition));
                        break;
                    }
                case 2:
                    {
                        int shift = 6 - (bitIndex & 6);
                        int mask = ~(0x3 << shift);
                        data[byteIndex] = (byte)((data[byteIndex] & mask) | ((sampleValue & 0x3) << shift));
                        break;
                    }
                case 4:
                    {
                        bool isHighNibble = (bitIndex & 4) == 0;
                        if (isHighNibble)
                        {
                            data[byteIndex] = (byte)((data[byteIndex] & 0x0F) | ((sampleValue & 0x0F) << 4));
                        }
                        else
                        {
                            data[byteIndex] = (byte)((data[byteIndex] & 0xF0) | (sampleValue & 0x0F));
                        }
                        break;
                    }
                case 8:
                    {
                        data[byteIndex] = (byte)sampleValue;
                        break;
                    }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int PaethPredictor(int a, int b, int c)
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
