using PdfPixel.Geometry;
using PdfPixel.Imaging;
using System;

namespace Benchmarks
{
    /// <summary>
    /// Checks that the library traces the same outlines as the baseline.
    /// </summary>
    internal static class StencilOutlineVerification
    {
        public static void Run()
        {
            int checkedCount = 0;

            foreach (string name in StencilMasks.Names)
            {
                CheckSame(StencilMasks.ToDecodedImage(StencilMasks.Create(name)));
                checkedCount++;
            }

            foreach (bool[,] painted in StencilMasks.CreateRandom(20000, seed: 11))
            {
                CheckSame(StencilMasks.ToDecodedImage(painted));
                checkedCount++;
            }

            Console.WriteLine($"{checkedCount} masks traced the same as the baseline.");
        }

        public static void CheckSame(PdfDecodedImage samples)
        {
            PdfPath? expected = StencilOutlineBaseline.Trace(samples);
            PdfPath? actual = PdfStencilOutline.Trace(samples);

            if (expected == null || actual == null)
            {
                if (expected != actual)
                {
                    throw new InvalidOperationException($"Mask {samples.Width}x{samples.Height}: baseline outline {Describe(expected)}, library outline {Describe(actual)}.");
                }

                return;
            }

            if (!expected.Buffer.Span.SequenceEqual(actual.Buffer.Span))
            {
                throw new InvalidOperationException($"Mask {samples.Width}x{samples.Height}: outlines differ ({expected.Buffer.Length} and {actual.Buffer.Length} bytes).");
            }
        }

        private static string Describe(PdfPath? path) => (path == null) ? "rejected" : $"{path.Buffer.Length} bytes";
    }
}
