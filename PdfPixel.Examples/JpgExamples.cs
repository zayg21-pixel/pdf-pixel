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
using PdfPixel.Jpg.Color;
using PdfPixel.Jpg.Decoding;
using PdfPixel.Jpg.Model;
using PdfPixel.Jpg.Readers;

namespace PdfPixel.Examples;

/// <summary>
/// Decoding JPEG images: at full size, at a reduced size, over a region of interest, and
/// converting a CMYK image to sRGB through the ICC profile its header carries.
/// </summary>
internal static class JpgExamples
{
    private const string Format = "Jpg";
    private const string SourceFile = "baboon.jpg";
    private const string GraySourceFile = "baboon-gray.jpg";
    private const string CmykSourceFile = "baboon-cmyk.jpg";

    /// <summary>
    /// Runs every JPEG example.
    /// </summary>
    public static void Run()
    {
        Decode(SourceFile, "at full size", "baboon.png", descaleFactor: 1, regionOfInterest: null);

        // The descale factor must be 1, 2, 4 or 8. Above 1 the decoder reconstructs fewer samples
        // per block instead of reconstructing and then throwing them away.
        Decode(SourceFile, "at a quarter of its size", "baboon-descale-4.png", descaleFactor: 4, regionOfInterest: null);

        // A region of interest is given in stored samples. Blocks outside it are neither inverse
        // transformed nor color converted, and the rows covering them come out blank.
        JpgRectangle eyes = new(96, 128, 320, 192);
        Decode(SourceFile, "over one region of interest", "baboon-region.png", descaleFactor: 1, eyes);

        // A single-component JPEG decodes to one gray sample per pixel.
        Decode(GraySourceFile, "at full size", "baboon-gray.png", descaleFactor: 1, regionOfInterest: null);

        DecodeCmykThroughEmbeddedProfile();
    }

    /// <summary>
    /// Decodes a JPEG to a PNG.
    /// </summary>
    /// <param name="sourceFileName">Name of the JPEG in the input folder.</param>
    /// <param name="description">Text naming this variant on the console.</param>
    /// <param name="outputFileName">Name of the PNG written to the output folder.</param>
    /// <param name="descaleFactor">Power-of-two reduction (1, 2, 4 or 8) applied to the decoded size.</param>
    /// <param name="regionOfInterest">Region to reconstruct, or null for the whole image.</param>
    private static void Decode(
        string sourceFileName,
        string description,
        string outputFileName,
        int descaleFactor,
        JpgRectangle? regionOfInterest)
    {
        Console.WriteLine($"[Jpg] Decoding {sourceFileName} {description}...");

        // The decoder works over the whole file in memory, header and entropy-coded data alike.
        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, sourceFileName));

        // Parses the marker segments up to the first scan: frame size, components, and tables.
        JpgHeader header = JpgReader.ParseHeader(fileBytes);

        JpgDecoderOptions options = new()
        {
            DescaleFactor = descaleFactor,
            RegionsOfInterest = (regionOfInterest == null) ? null : new List<JpgRectangle> { regionOfInterest.Value }
        };

        // Output size, stride, and MCU layout, all derived from the header.
        JpgDecodingParameters decodingParameters = new(header, options.DescaleFactor, options.RegionsOfInterest);

        // Picks the baseline or the progressive decoder to match the frame type in the header.
        IJpgDecoder decoder = JpgDecoderFactory.Create(header, fileBytes, options);

        // Rows arrive one at a time as interleaved samples, one byte per component.
        var samples = new byte[decodingParameters.OutputStride * decodingParameters.OutputHeight];
        var rowBuffer = new byte[decodingParameters.OutputStride];

        for (int row = 0; row < decodingParameters.OutputHeight && decoder.TryReadRow(rowBuffer); row++)
        {
            rowBuffer.CopyTo(samples.AsSpan(row * decodingParameters.OutputStride));
        }

        // A single-component JPEG is gray, three are RGB. OutputStride is one row of samples.
        PngColorType colorType = (header.ComponentCount == 1) ? PngColorType.Gray : PngColorType.Truecolor;
        string outputPath = ExamplePaths.Output(Format, outputFileName);
        PngWriter.Write(outputPath, decodingParameters.OutputWidth, decodingParameters.OutputHeight, bitDepth: 8, colorType, decodingParameters.OutputStride, samples);

        Console.WriteLine($"[Jpg]   {decodingParameters.OutputWidth}x{decodingParameters.OutputHeight} -> {ExamplePaths.Relative(outputPath)}");
    }

    /// <summary>
    /// Decodes a CMYK JPEG and converts its samples to sRGB with the ICC profile from its APP2 segments.
    /// </summary>
    private static void DecodeCmykThroughEmbeddedProfile()
    {
        Console.WriteLine($"[Jpg] Decoding {CmykSourceFile} through its embedded ICC profile...");

        byte[] fileBytes = File.ReadAllBytes(ExamplePaths.Input(Format, CmykSourceFile));
        JpgHeader header = JpgReader.ParseHeader(fileBytes);

        // A CMYK profile is split over several APP2 segments, which this reassembles into one profile.
        if (!JpgIccProfileReader.TryAssembleIccProfile(header, out byte[]? profileBytes) || profileBytes == null)
        {
            Console.WriteLine($"[Jpg]   {CmykSourceFile} carries no ICC profile.");
            return;
        }

        // IccProfileTransform builds the pipeline that takes the profile's own color space to sRGB.
        IccProfile profile = IccProfile.Parse(profileBytes);
        IccProfileTransform profileTransform = new(profile);
        ChainedColorTransform toSrgb = profileTransform.GetIntentTransform(IccRenderingIntent.Perceptual);
        ColorTransformSampler sampler = new(toSrgb);

        // Standalone CMYK JPEGs written by Adobe tools store inverted ink values, which the default
        // options undo; inside the PDF pipeline the same file is decoded with InvertCmykColors off.
        JpgDecoderOptions options = new() { InvertCmykColors = true };
        JpgDecodingParameters decodingParameters = new(header, options.DescaleFactor, options.RegionsOfInterest);
        IJpgDecoder decoder = JpgDecoderFactory.Create(header, fileBytes, options);

        var rowBuffer = new byte[decodingParameters.OutputStride];
        var pixels = new RgbPacked[decodingParameters.OutputWidth * decodingParameters.OutputHeight];
        var components = new float[header.ComponentCount];

        for (int row = 0; row < decodingParameters.OutputHeight && decoder.TryReadRow(rowBuffer); row++)
        {
            Span<RgbPacked> pixelRow = pixels.AsSpan(row * decodingParameters.OutputWidth, decodingParameters.OutputWidth);

            for (int x = 0; x < decodingParameters.OutputWidth; x++)
            {
                // The sampler takes components in the 0-1 range and returns straight sRGB, also 0-1.
                for (int component = 0; component < components.Length; component++)
                {
                    components[component] = rowBuffer[(x * header.ComponentCount) + component] / 255f;
                }

                Vector4 color = sampler.Sample(components);

                // Scales, clamps and rounds the four lanes at once, then stores three of them.
                ColorVectorUtilities.Load01ToRgb(color, ref pixelRow[x]);
            }
        }

        // RgbPacked is three tightly-packed bytes, so the buffer reinterprets as 24-bit PNG rows.
        ReadOnlySpan<byte> rows = MemoryMarshal.AsBytes<RgbPacked>(pixels);

        string outputPath = ExamplePaths.Output(Format, "baboon-cmyk-srgb.png");
        PngWriter.Write(outputPath, decodingParameters.OutputWidth, decodingParameters.OutputHeight, bitDepth: 8, PngColorType.Truecolor, decodingParameters.OutputWidth * 3, rows);

        Console.WriteLine(
            $"[Jpg]   {profile.Header.ColorSpace} profile, {decodingParameters.OutputWidth}x{decodingParameters.OutputHeight} -> {ExamplePaths.Relative(outputPath)}");
    }
}
