using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using SkiaSharp;
using PdfReader.Rendering.Image.Ccitt;

namespace PdfReader.Tests
{
    /// <summary>
    /// Tests for CCITT Group 3 mixed 1D/2D decoder (K parameter variant).
    /// Verifies decoded result against reference PNG via pixel comparison.
    /// </summary>
    public class CcittG32DTests
    {
        private struct ImageData
        {
            public int width;
            public int height;
            public string hexData;
            public string fileName;
            public bool isBlackLs;
            public int k;
            public bool endOfLine;
            public bool byteAlign;
        }

        private readonly ImageData testFile1 = new ImageData
        {
            width = 81,
            height = 26,
            fileName = "ccittk1.png",
            hexData = "0019A81E0C000A56F5800BC0059AE2ECCCCD76002FD25C005A5A4B800BF4970017E905C005FA4B800B4B4A58FC005FA55E002F8B85E002FC7C005A57E002F9277E002FA7F0017D5F800B4B7F0017F7E002FDFC005FB7E002D2EFC005FBF800B1111C005E0028C0",
            isBlackLs = false,
            k = 1,
            endOfLine = false,
            byteAlign = false
        };

        [Fact]
        public void DecodeTestFile1_ShouldMatchReferencePng()
        {
            // Arrange
            var testData = testFile1;
            var encodedBytes = HexStringToBytes(testData.hexData);

            // Act
            var packed = DecodeToPackedG32D(encodedBytes, testData.width, testData.height, testData.isBlackLs, testData.k, testData.endOfLine, testData.byteAlign);
            var decodedRgba = ExpandPackedToRgba(packed, testData.width, testData.height, testData.isBlackLs);

            // Assert
            Assert.NotNull(decodedRgba);
            Assert.Equal(testData.width * testData.height * 4, decodedRgba.Length);

            // Load reference PNG and compare pixel by pixel
            var referencePath = GetTestImagePath(testData.fileName);
            using var referenceBitmap = LoadPngImage(referencePath);
            ComparePixelByPixel(decodedRgba, referenceBitmap, testData.width, testData.height, testData.fileName);
        }

        private static byte[] DecodeToPackedG32D(ReadOnlySpan<byte> encoded, int width, int height, bool blackIs1, int k, bool endOfLine, bool byteAlign)
        {
            int rowBytes = (width + 7) / 8;
            byte[] packed = new byte[rowBytes * height];
            CcittG32DDecoder.Decode(encoded, packed.AsSpan(), width, height, blackIs1, k, endOfLine, byteAlign);
            return packed;
        }

        private static byte[] ExpandPackedToRgba(byte[] packed, int width, int height, bool blackIs1)
        {
            byte[] rgba = new byte[width * height * 4];
            int rowBytes = (width + 7) / 8;
            int rgbaIndex = 0;
            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                int rowOffset = rowIndex * rowBytes;
                for (int columnIndex = 0; columnIndex < width; columnIndex++)
                {
                    int byteIndex = rowOffset + (columnIndex >> 3);
                    int bitIndex = 7 - (columnIndex & 7);
                    int bit = (packed[byteIndex] >> bitIndex) & 1;
                    bool isBlack = bit == (blackIs1 ? 1 : 0);
                    byte value = isBlack ? (byte)0 : (byte)255;
                    rgba[rgbaIndex++] = value;
                    rgba[rgbaIndex++] = value;
                    rgba[rgbaIndex++] = value;
                    rgba[rgbaIndex++] = 255;
                }
            }
            return rgba;
        }

        /// <summary>
        /// Convert hex string to byte array.
        /// </summary>
        private static byte[] HexStringToBytes(string hexString)
        {
            if (hexString.Length % 2 != 0)
            {
                throw new ArgumentException("Hex string must have even length", nameof(hexString));
            }

            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hexString.Substring(i * 2, 2), 16);
            }
            return bytes;
        }

        /// <summary>
        /// Get the full path to a test image file.
        /// </summary>
        private static string GetTestImagePath(string fileName)
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var imagePath = Path.Combine(currentDirectory, fileName);
            
            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException($"Reference image file not found: {imagePath}");
            }
            
            return imagePath;
        }

        /// <summary>
        /// Load a PNG image using SkiaSharp.
        /// </summary>
        private static SKBitmap LoadPngImage(string filePath)
        {
            using var fileStream = File.OpenRead(filePath);
            var bitmap = SKBitmap.Decode(fileStream);
            
            if (bitmap == null)
            {
                throw new InvalidOperationException($"Failed to load PNG image: {filePath}");
            }
            
            return bitmap;
        }

        /// <summary>
        /// Compare decoded RGBA buffer with reference PNG image pixel by pixel.
        /// </summary>
        private static void ComparePixelByPixel(byte[] decodedRgba, SKBitmap referenceBitmap, int width, int height, string testFileName)
        {
            // Check dimensions
            if (width != referenceBitmap.Width)
            {
                Assert.True(false, $"Width mismatch for {testFileName}: expected {width}, got {referenceBitmap.Width}");
            }
            
            if (height != referenceBitmap.Height)
            {
                Assert.True(false, $"Height mismatch for {testFileName}: expected {height}, got {referenceBitmap.Height}");
            }

            int mismatchCount = 0;
            const int maxMismatchesToReport = 10;
            var mismatchDetails = new List<string>();

            for (int rowIndex = 0; rowIndex < height; rowIndex++)
            {
                for (int columnIndex = 0; columnIndex < width; columnIndex++)
                {
                    int decodedOffset = (rowIndex * width + columnIndex) * 4;
                    byte decodedR = decodedRgba[decodedOffset];
                    byte decodedG = decodedRgba[decodedOffset + 1];
                    byte decodedB = decodedRgba[decodedOffset + 2];
                    byte decodedA = decodedRgba[decodedOffset + 3];

                    var referencePixel = referenceBitmap.GetPixel(columnIndex, rowIndex);

                    // Compare RGB values (ignore slight alpha differences)
                    if (decodedR != referencePixel.Red || decodedG != referencePixel.Green || decodedB != referencePixel.Blue)
                    {
                        mismatchCount++;
                        if (mismatchDetails.Count < maxMismatchesToReport)
                        {
                            mismatchDetails.Add(
                                $"Pixel ({columnIndex},{rowIndex}): decoded=RGB({decodedR},{decodedG},{decodedB},A:{decodedA}) " +
                                $"vs reference=RGB({referencePixel.Red},{referencePixel.Green},{referencePixel.Blue},A:{referencePixel.Alpha})");
                        }
                    }
                }
            }

            if (mismatchCount > 0)
            {
                string errorMessage = $"Pixel comparison failed for {testFileName}. Total mismatches: {mismatchCount} out of {width * height} pixels.\n" +
                    $"First {Math.Min(mismatchCount, maxMismatchesToReport)} mismatches:\n" + string.Join("\n", mismatchDetails);
                if (mismatchCount > maxMismatchesToReport)
                {
                    errorMessage += $"\n... and {mismatchCount - maxMismatchesToReport} more mismatches.";
                }
                Assert.True(false, errorMessage);
            }
        }
    }
}
