using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using SkiaSharp;
using PdfReader.Rendering.Image.Ccitt;
using System.Diagnostics;

namespace PdfReader.Tests
{
    /// <summary>
    /// Tests for CCITT Group 4 two-dimensional decoder.
    /// Verifies correct decoding by comparing decoded results pixel-by-pixel with reference PNG images.
    /// </summary>
    public class CcittG4Tests
    {
        private struct ImageData
        {
            public int width;
            public int height;
            public string hexData;
            public string fileName;
            public bool isBlackLs;
        }

        private readonly ImageData testFile1 = new ImageData
        {
            width = 134,
            height = 39,
            fileName = "ccitt.png",
            hexData = "91C321E549FFFFF9E8BA39174472238A5C523886A148D5F08105081050C294E081653820A19EE19E1BFF7DFFFFFDBFE93A4F8E238E1043B7FF1C7FF36CBB23DE619B66182987FFFE3408130F0BD022FA0447D022FA3BD022FFFFF822E87BFFC7FFFFFFF82BFC7FFC7F6C1150CE2FF1477DE94FDFF9E9FA5A5FFE8111FD2C114F0453FFCC2F86834106106106106C47114C1B0D8A62D83F8888888888888FFFFFFC8688E2178D51E234C90CFDFC33DB770C254088FBFD2FFD0223F8FFFFF1FFFE96619B797B36F4CC3CC330FE94568117D0223FC2D022EB69022FE811748117FFCF0F1F1D2EC7C7EC7FFEDE0BC7B7E0BB71FFFD1DFC223FB7E8EFDB9C1FA5FFE08A7B78453FFF08A7E617F08526C5B0D3887141069B11714C1FC4444444444447FFFF8E30010010",
            isBlackLs = false
        };

        private readonly ImageData testFile2 = new ImageData
        {
            width = 134,
            height = 39,
            fileName = "ccittBlackIs.png",
            hexData = "26A19052A57C8E191FFFFFF25071C9C1C72B8A99E655082F320D31F840828408286148F820591F041432EE1979BFFBEFFFFFEDFFA4E93E388E38410EDFFE38FFE5D9ECA8F359766B0535FFFFC6810261E17A0477408A7408EE8BF408EFFFFF0471C7BFFC7FFFFFFFC15FE3FFE3FDB04479931FFC517F7A52E5FF9713FA5A5FFE8114FA58223F0447FFE61FF0D06820C20C20C20D88E2298361B14C5B07F8888888888888FFFFFFFE41072A87820BC12E223FF865DB770C252147FD2FFD0229FFFFFC75FFD2CD65DE78CBBD19A388C46C0C7D28AD023BA0453F0B408E3ECBCCA1C15D023BFFCBCF1F1D2EC7E87FFFF6F05E3DBFFE3FFFD17FC229F6FFF970CCFA5FFE088FDBC223FF0817F987FF08526C5B0D388714106DA6C5B07F8888888888888FFFFFF1E3001001",
            isBlackLs = true
        };

        [Fact]
        public void DecodeTestFile1_ShouldMatchReferencePng()
        {
            var testData = testFile1;
            var encodedBytes = HexStringToBytes(testData.hexData);

            var packed = DecodeToPackedG4(encodedBytes, testData.width, testData.height, testData.isBlackLs, endOfBlock: false);
            var decodedRgba = ExpandPackedToRgba(packed, testData.width, testData.height, testData.isBlackLs);

            Assert.NotNull(decodedRgba);
            Assert.Equal(testData.width * testData.height * 4, decodedRgba.Length);

            var referencePath = GetTestImagePath(testData.fileName);
            using var referenceBitmap = LoadPngImage(referencePath);
            ComparePixelByPixel(decodedRgba, referenceBitmap, testData.width, testData.height, testData.fileName);
        }

        [Fact]
        public void DecodeTestFile2_ShouldMatchReferencePng()
        {
            var testData = testFile2;
            var encodedBytes = HexStringToBytes(testData.hexData);

            var packed = DecodeToPackedG4(encodedBytes, testData.width, testData.height, testData.isBlackLs, endOfBlock: false);
            var decodedRgba = ExpandPackedToRgba(packed, testData.width, testData.height, testData.isBlackLs);

            Assert.NotNull(decodedRgba);
            Assert.Equal(testData.width * testData.height * 4, decodedRgba.Length);

            var referencePath = GetTestImagePath(testData.fileName);
            using var referenceBitmap = LoadPngImage(referencePath);
            ComparePixelByPixel(decodedRgba, referenceBitmap, testData.width, testData.height, testData.fileName);
        }

        private static byte[] DecodeToPackedG4(ReadOnlySpan<byte> encoded, int width, int height, bool blackIs1, bool endOfBlock)
        {
            int rowBytes = (width + 7) / 8;
            byte[] packed = new byte[rowBytes * height];
            CcittG4TwoDDecoder.Decode(encoded, packed.AsSpan(), width, height, blackIs1, endOfBlock);
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

        private static void ComparePixelByPixel(byte[] decodedRgba, SKBitmap referenceBitmap, int width, int height, string testFileName)
        {
            if (width != referenceBitmap.Width)
            {
                Assert.Fail($"Width mismatch for {testFileName}: expected {width}, got {referenceBitmap.Width}");
            }

            if (height != referenceBitmap.Height)
            {
                Assert.Fail($"Height mismatch for {testFileName}: expected {height}, got {referenceBitmap.Height}");
            }

            int mismatchCount = 0;
            const int maxMismatchesToReport = 10;
            var mismatchDetails = new List<string>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int decodedOffset = (y * width + x) * 4;
                    var decodedR = decodedRgba[decodedOffset];
                    var decodedG = decodedRgba[decodedOffset + 1];
                    var decodedB = decodedRgba[decodedOffset + 2];
                    var decodedA = decodedRgba[decodedOffset + 3];

                    var referencePixel = referenceBitmap.GetPixel(x, y);

                    if (decodedR != referencePixel.Red ||
                        decodedG != referencePixel.Green ||
                        decodedB != referencePixel.Blue)
                    {
                        mismatchCount++;

                        if (mismatchDetails.Count < maxMismatchesToReport)
                        {
                            mismatchDetails.Add(
                                $"Pixel ({x},{y}): decoded=RGB({decodedR},{decodedG},{decodedB},A:{decodedA}) " +
                                $"vs reference=RGB({referencePixel.Red},{referencePixel.Green},{referencePixel.Blue},A:{referencePixel.Alpha})");
                        }
                    }
                }
            }

            if (mismatchCount > 0)
            {
                var errorMessage = $"Pixel comparison failed for {testFileName}. " +
                                   $"Total mismatches: {mismatchCount} out of {width * height} pixels.\n" +
                                   $"First {Math.Min(mismatchCount, maxMismatchesToReport)} mismatches:\n" +
                                   string.Join("\n", mismatchDetails);

                if (mismatchCount > maxMismatchesToReport)
                {
                    errorMessage += $"\n... and {mismatchCount - maxMismatchesToReport} more mismatches.";
                }

                Assert.Fail(errorMessage);
            }
        }
    }
}
