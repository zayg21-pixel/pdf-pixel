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
    /// Tests for CCITT Group 3 mixed 1D/2D decoder.
    /// Verifies correct decoding by comparing decoded results pixel-by-pixel with reference PNG images.
    /// Also includes LibTiff comparison to debug decoder differences.
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

        /// <summary>
        /// Test decoding of first test file with specified parameters.
        /// Compares decoded result pixel-by-pixel with reference PNG image.
        /// </summary>
        [Fact]
        public void DecodeTestFile1_ShouldMatchReferencePng()
        {
            // Arrange
            var testData = testFile1;
            var encodedBytes = HexStringToBytes(testData.hexData);
            
            // Act
            var decodedRgba = CcittG32DDecoder.Decode(encodedBytes, testData.width, testData.height, 
                testData.isBlackLs, testData.k, testData.endOfLine, testData.byteAlign);
            
            // Assert
            Assert.NotNull(decodedRgba);
            Assert.Equal(testData.width * testData.height * 4, decodedRgba.Length);
            
            // Load reference PNG and compare pixel by pixel
            var referencePath = GetTestImagePath(testData.fileName);
            using var referenceBitmap = LoadPngImage(referencePath);
            ComparePixelByPixel(decodedRgba, referenceBitmap, testData.width, testData.height, testData.fileName);
        }

        /// <summary>
        /// Save RGBA byte array as PNG file for visual inspection.
        /// </summary>
        private static void SaveRgbaAsPng(byte[] rgbaData, int width, int height, string filename)
        {
            try
            {
                var currentDirectory = Directory.GetCurrentDirectory();
                var outputPath = Path.Combine(currentDirectory, filename);
                
                using var bitmap = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                
                // Copy RGBA data to SKBitmap
                unsafe
                {
                    var pixels = (byte*)bitmap.GetPixels();
                    for (int i = 0; i < rgbaData.Length; i++)
                    {
                        pixels[i] = rgbaData[i];
                    }
                }
                
                using var image = SKImage.FromBitmap(bitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(outputPath);
                data.SaveTo(stream);
                
                Console.WriteLine($"LibTiff decoded result saved as: {outputPath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to save LibTiff result as PNG: {ex.Message}");
            }
        }

        /// <summary>
        /// Compare two decoded RGBA buffers pixel by pixel.
        /// </summary>
        private static void CompareDecodedResults(byte[] ourResult, byte[] referenceResult, int width, int height, string referenceName)
        {
            int mismatchCount = 0;
            const int maxMismatchesToReport = 10;
            var mismatchDetails = new List<string>();

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Get our decoded pixel (RGBA format)
                    int ourOffset = (y * width + x) * 4;
                    var ourR = ourResult[ourOffset];
                    var ourG = ourResult[ourOffset + 1];
                    var ourB = ourResult[ourOffset + 2];
                    var ourA = ourResult[ourOffset + 3];

                    // Get reference decoded pixel (RGBA format)
                    int refOffset = (y * width + x) * 4;
                    var refR = referenceResult[refOffset];
                    var refG = referenceResult[refOffset + 1];
                    var refB = referenceResult[refOffset + 2];
                    var refA = referenceResult[refOffset + 3];
                    
                    // Compare RGB values (ignore slight alpha differences)
                    if (ourR != refR || ourG != refG || ourB != refB)
                    {
                        mismatchCount++;
                        
                        if (mismatchDetails.Count < maxMismatchesToReport)
                        {
                            mismatchDetails.Add(
                                $"Pixel ({x},{y}): our=RGB({ourR},{ourG},{ourB},A:{ourA}) " +
                                $"vs {referenceName}=RGB({refR},{refG},{refB},A:{refA})");
                        }
                    }
                }
            }

            if (mismatchCount > 0)
            {
                var errorMessage = $"Decoder comparison failed against {referenceName}. " +
                                 $"Total mismatches: {mismatchCount} out of {width * height} pixels.\n" +
                                 $"First {Math.Min(mismatchCount, maxMismatchesToReport)} mismatches:\n" +
                                 string.Join("\n", mismatchDetails);
                
                if (mismatchCount > maxMismatchesToReport)
                {
                    errorMessage += $"\n... and {mismatchCount - maxMismatchesToReport} more mismatches.";
                }
                
                Assert.True(false, errorMessage);
            }
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

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    // Get decoded pixel (RGBA format)
                    int decodedOffset = (y * width + x) * 4;
                    var decodedR = decodedRgba[decodedOffset];
                    var decodedG = decodedRgba[decodedOffset + 1];
                    var decodedB = decodedRgba[decodedOffset + 2];
                    var decodedA = decodedRgba[decodedOffset + 3];

                    // Get reference pixel
                    var referencePixel = referenceBitmap.GetPixel(x, y);
                    
                    // Compare RGB values (ignore slight alpha differences)
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
                
                Assert.True(false, errorMessage);
            }
        }
    }
}
