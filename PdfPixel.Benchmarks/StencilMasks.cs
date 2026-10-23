using Microsoft.Extensions.Logging.Abstractions;
using PdfPixel;
using PdfPixel.Imaging;
using PdfPixel.Skia.Fonts;
using PdfPixel.Objects;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace Benchmarks
{
    /// <summary>
    /// Synthetic stencil masks of the shapes the outline tracing meets, and a document that holds one as an image XObject.
    /// </summary>
    internal static class StencilMasks
    {
        public const string Glyph = "Glyph 64x64";
        public const string Text = "Text 300x300";
        public const string Blobs = "Blobs 1000x1000";
        public const string Noise = "Noise 1000x1000";

        public static readonly string[] Names = [Glyph, Text, Blobs, Noise];

        /// <summary>
        /// Returns the painted samples of the named mask, row by row from the top.
        /// </summary>
        public static bool[,] Create(string name)
        {
            switch (name)
            {
                case Glyph:
                {
                    return CreateGlyph(64, 64);
                }
                case Text:
                {
                    return CreateText(300, 300);
                }
                case Blobs:
                {
                    return CreateBlobs(1000, 1000);
                }
                case Noise:
                {
                    return CreateNoise(1000, 1000, seed: 7);
                }
                default:
                {
                    throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown mask.");
                }
            }
        }

        /// <summary>
        /// Returns random masks of random sizes, some with diagonal touches, for checking outlines against the baseline.
        /// </summary>
        public static IEnumerable<bool[,]> CreateRandom(int count, int seed)
        {
            Random random = new(seed);

            for (int index = 0; index < count; index++)
            {
                int width = random.Next(1, 80);
                int height = random.Next(1, 80);
                double density = random.NextDouble();
                var painted = new bool[height, width];

                for (int row = 0; row < height; row++)
                {
                    for (int column = 0; column < width; column++)
                    {
                        painted[row, column] = random.NextDouble() < density;
                    }
                }

                yield return painted;
            }
        }

        /// <summary>
        /// Returns <paramref name="painted"/> decoded as the library decodes a stencil mask: one alpha byte per sample.
        /// </summary>
        public static PdfDecodedImage ToDecodedImage(bool[,] painted)
        {
            int height = painted.GetLength(0);
            int width = painted.GetLength(1);
            PdfDecodedImage decoded = new(width, height, PdfImageColorFormat.Alpha, PdfImageAlphaType.Unpremultiplied);
            Span<byte> buffer = decoded.GetRawBuffer();

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    buffer[(row * width) + column] = painted[row, column] ? (byte)255 : (byte)0;
                }
            }

            return decoded;
        }

        /// <summary>
        /// Returns a document with one page whose resources hold <paramref name="painted"/> as the Flate encoded stencil
        /// mask image XObject /Im0.
        /// </summary>
        public static PdfDocument CreateDocument(bool[,] painted)
        {
            int height = painted.GetLength(0);
            int width = painted.GetLength(1);
            int rowBytes = (width + 7) / 8;
            var packed = new byte[rowBytes * height];

            // A stencil sample of 0 paints, under the default /Decode [0 1]; the bits of unpainted samples are set.
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    if (!painted[row, column])
                    {
                        packed[(row * rowBytes) + (column >> 3)] |= (byte)(0x80 >> (column & 7));
                    }
                }
            }

            using MemoryStream compressed = new();
            using (ZLibStream zlib = new(compressed, CompressionLevel.Optimal, leaveOpen: true))
            {
                zlib.Write(packed);
            }

            byte[] imageData = compressed.ToArray();
            string imageHeader = $"<< /Type /XObject /Subtype /Image /Width {width} /Height {height} /ImageMask true /BitsPerComponent 1 /Filter /FlateDecode /Length {imageData.Length} >>\nstream\n";

            MemoryStream file = new();
            List<long> offsets = [];

            WriteAscii(file, "%PDF-1.7\n");
            offsets.Add(file.Position);
            WriteAscii(file, "1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n");
            offsets.Add(file.Position);
            WriteAscii(file, "2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n");
            offsets.Add(file.Position);
            WriteAscii(file, "3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Resources << /XObject << /Im0 4 0 R >> >> >>\nendobj\n");
            offsets.Add(file.Position);
            WriteAscii(file, "4 0 obj\n" + imageHeader);
            file.Write(imageData);
            WriteAscii(file, "\nendstream\nendobj\n");

            long crossReferenceOffset = file.Position;
            StringBuilder crossReference = new();
            crossReference.Append("xref\n0 5\n0000000000 65535 f \n");

            foreach (long offset in offsets)
            {
                crossReference.Append(offset.ToString("D10")).Append(" 00000 n \n");
            }

            crossReference.Append("trailer\n<< /Size 5 /Root 1 0 R >>\nstartxref\n").Append(crossReferenceOffset).Append("\n%%EOF\n");
            WriteAscii(file, crossReference.ToString());
            file.Position = 0;

            SkiaFontSubstitutor fontSubstitutor = new(NullLoggerFactory.Instance);

            return new PdfDocument(NullLoggerFactory.Instance, fontSubstitutor, file, null);
        }

        /// <summary>
        /// Returns the image XObject /Im0 of the first page of <paramref name="document"/>.
        /// </summary>
        public static PdfImage GetImage(PdfDocument document)
        {
            PdfImage? image = document.Pages[0].PageResources.Resources?.GetXObject(PdfString.FromString("Im0")) as PdfImage;

            return image ?? throw new InvalidOperationException("The document holds no image /Im0.");
        }

        private static void WriteAscii(Stream stream, string text) => stream.Write(Encoding.ASCII.GetBytes(text));

        private static bool[,] CreateGlyph(int width, int height)
        {
            var painted = new bool[height, width];

            // A bowl with a counter and a stem, as a bitmap glyph of a Type3 font is drawn.
            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    double horizontal = (column - 38) / 20.0;
                    double vertical = (row - 40) / 18.0;
                    double distance = (horizontal * horizontal) + (vertical * vertical);
                    bool bowl = distance <= 1 && distance >= 0.45;
                    bool stem = column >= 8 && column < 18 && row >= 4 && row < 60;
                    painted[row, column] = bowl || stem;
                }
            }

            return painted;
        }

        private static bool[,] CreateText(int width, int height)
        {
            var painted = new bool[height, width];
            bool[,] glyph = CreateGlyph(64, 64);
            const int Scale = 4;

            // Lines of 16x16 glyphs, each the glyph above sampled down, with a gap between glyphs and lines.
            for (int lineTop = 2; lineTop + 16 <= height; lineTop += 22)
            {
                for (int glyphLeft = 2; glyphLeft + 16 <= width; glyphLeft += 19)
                {
                    for (int row = 0; row < 16; row++)
                    {
                        for (int column = 0; column < 16; column++)
                        {
                            painted[lineTop + row, glyphLeft + column] = glyph[row * Scale, column * Scale];
                        }
                    }
                }
            }

            return painted;
        }

        private static bool[,] CreateBlobs(int width, int height)
        {
            var painted = new bool[height, width];
            Random random = new(3);
            var centers = new (double Column, double Row, double RadiusX, double RadiusY)[12];

            for (int index = 0; index < centers.Length; index++)
            {
                centers[index] = (random.Next(100, width - 100), random.Next(100, height - 100), random.Next(30, 120), random.Next(30, 120));
            }

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    foreach ((double centerColumn, double centerRow, double radiusX, double radiusY) in centers)
                    {
                        double horizontal = (column - centerColumn) / radiusX;
                        double vertical = (row - centerRow) / radiusY;

                        if ((horizontal * horizontal) + (vertical * vertical) <= 1)
                        {
                            painted[row, column] = true;
                            break;
                        }
                    }
                }
            }

            return painted;
        }

        private static bool[,] CreateNoise(int width, int height, int seed)
        {
            var painted = new bool[height, width];
            Random random = new(seed);

            for (int row = 0; row < height; row++)
            {
                for (int column = 0; column < width; column++)
                {
                    painted[row, column] = random.Next(2) == 0;
                }
            }

            return painted;
        }
    }
}
