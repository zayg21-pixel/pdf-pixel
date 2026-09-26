using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using PdfPixel.Color.Icc;
using PdfPixel.Color.Icc.Model;
using PdfPixel.Color.Sampling;
using PdfPixel.Color.Structures;
using PdfPixel.Color.Transform;
using PdfPixel.Tiff.Decoding;
using PdfPixel.Tiff.Model;
using PdfPixel.Tiff.Parsing;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding a CMYK TIFF and converting it to sRGB through the ICC profile its directory carries.
/// </summary>
internal static class TiffExamples
{
    private const string Format = "Tiff";
    private const string CmykSourceFile = "baboon-cmyk.tif";

    /// <summary>
    /// Runs every TIFF example.
    /// </summary>
    public static void Run() => DecodeCmykThroughEmbeddedProfile();

    /// <summary>
    /// Decodes a CMYK TIFF and converts its samples to sRGB with the ICC profile from its InterColorProfile tag.
    /// </summary>
    private static void DecodeCmykThroughEmbeddedProfile()
    {
        Console.WriteLine($"[Tiff] Decoding {CmykSourceFile} through its embedded ICC profile...");

        // The decoder works over the whole file in memory: strips and tiles are addressed by file offset.
        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, CmykSourceFile));

        // Reads every image file directory in the file; the first one describes the first image.
        List<TiffDirectory> directories = TiffReader.ReadDirectories(fileBytes);
        TiffDirectory directory = directories[0];

        if (directory.IccProfile == null)
        {
            Console.WriteLine($"[Tiff]   {CmykSourceFile} carries no ICC profile.");
            return;
        }

        // IccProfileTransform builds the pipeline that takes the profile's own color space to sRGB.
        IccProfile profile = IccProfile.Parse(directory.IccProfile);
        IccProfileTransform profileTransform = new(profile);
        ChainedColorTransform toSrgb = profileTransform.GetIntentTransform(IccRenderingIntent.Perceptual);
        ColorTransformSampler sampler = new(toSrgb);

        // Decompresses strips or tiles as rows are reached, and undoes the predictor this file was written with.
        TiffRowDecoder decoder = new(directory, fileBytes);

        // Rows arrive as interleaved samples at BitsPerComponent, one byte per ink for this 8-bit file.
        var rowBuffer = new byte[decoder.RowBytes];
        var pixels = new RgbPacked[decoder.Width * decoder.Height];
        var components = new float[decoder.ComponentCount];

        for (int row = 0; row < decoder.Height && decoder.TryReadRow(rowBuffer); row++)
        {
            Span<RgbPacked> pixelRow = pixels.AsSpan(row * decoder.Width, decoder.Width);

            for (int x = 0; x < decoder.Width; x++)
            {
                // The sampler takes ink amounts in the 0-1 range and returns straight sRGB, also 0-1.
                for (int component = 0; component < components.Length; component++)
                {
                    components[component] = rowBuffer[(x * decoder.ComponentCount) + component] / 255f;
                }

                Vector4 color = sampler.Sample(components);

                // Scales, clamps and rounds the four lanes at once, then stores three of them.
                ColorVectorUtilities.Load01ToRgb(color, ref pixelRow[x]);
            }
        }

        // RgbPacked is three tightly-packed bytes, so the buffer reinterprets as 24-bit PNG rows.
        ReadOnlySpan<byte> rows = MemoryMarshal.AsBytes<RgbPacked>(pixels);

        string outputPath = ExamplePaths.Output(Format, "baboon-cmyk-srgb.png");
        PngWriter.Write(outputPath, decoder.Width, decoder.Height, bitDepth: 8, PngColorType.Truecolor, decoder.Width * 3, rows);

        Console.WriteLine(
            $"[Tiff]   {profile.Header.ColorSpace} profile, {decoder.Width}x{decoder.Height} -> {ExamplePaths.Relative(outputPath)}");
    }
}
