using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands.Execution;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Objects;
using PdfPixel.Skia.Cache;
using System;
using System.Collections.Generic;

namespace PdfPixel.Skia;

/// <summary>
/// Creates the row processing pipelines that produce image tiles as Skia draws them: alpha-only stencils,
/// gray, and RGBA, with 8-bit samples.
/// </summary>
internal sealed class SkiaImagePipelineFactory : IPdfImagePipelineFactory
{
    private const int RgbComponentCount = 3;
    private const int ByteBitsPerComponent = 8;
    private const int FloatBitsPerComponent = 32;

    private static readonly PdfImageChannel[] AlphaChannels = [PdfImageChannel.Alpha];
    private static readonly PdfImageChannel[] GrayChannels = [PdfImageChannel.Component(0)];
    private static readonly PdfImageChannel[] RgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

    private readonly PdfCommandExecutionContext? _executionContext;

    /// <summary>
    /// Initializes a factory taking color samplers from the cache of <paramref name="executionContext"/>, or creating
    /// them for each pipeline when it is null.
    /// </summary>
    public SkiaImagePipelineFactory(PdfCommandExecutionContext? executionContext) => _executionContext = executionContext;

    /// <inheritdoc />
    public PdfImageRowProcessingPipeline CreateImagePipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion,
        IPdfAlphaRowSource? alphaSource)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        if (parameters.HasImageMask)
        {
            return CreateStencilPipeline(parameters, sourceRegion, outputRegion);
        }
        else
        {
            return CreateColorPipeline(parameters, sourceRegion, outputRegion, alphaSource);
        }
    }

    /// <inheritdoc />
    public PdfImageRowProcessingPipeline CreateMaskPipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion)
    {
        if (parameters == null)
        {
            throw new ArgumentNullException(nameof(parameters));
        }

        int bitsPerComponent = parameters.BitsPerComponent;
        List<IPdfImageRowProcessingStep> steps = [new PdfImageRowCropStep(bitsPerComponent, 1, sourceRegion)];

        if (parameters.HasImageMask)
        {
            if (StencilPaintsOnZero(parameters))
            {
                steps.Add(new PdfImageRowInvertStep(sourceRegion.Width, 1, bitsPerComponent));
            }

            AddUnpackAndResize(steps, 1, bitsPerComponent, sourceRegion, outputRegion);
        }
        else
        {
            AddUnpackAndResize(steps, 1, bitsPerComponent, sourceRegion, outputRegion);

            PdfRange[] decode = GetDecode(parameters, parameters.ImagePaint.DeviceColorMapping.Map(parameters.ImagePaint.ColorSpace), 1);

            if (!IsDefaultDecode(decode))
            {
                PdfColorSampler decodeSampler = new([new PdfDecodeColorTransform(decode)]);
                steps.Add(new PdfImageRowColorStep(decodeSampler, outputRegion.Width, 1, 1, hasAlpha: false));
            }
        }

        PdfImageSamples samples = new(outputRegion.Width, outputRegion.Height, PdfImageSampleFormat.Float, FloatBitsPerComponent, AlphaChannels);

        return new PdfImageRowProcessingPipeline(steps.ToArray(), samples);
    }

    private PdfImageRowProcessingPipeline CreateColorPipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion,
        IPdfAlphaRowSource? alphaSource)
    {
        PdfColorSpaceConverter colorSpace = parameters.ImagePaint.DeviceColorMapping.Map(parameters.ImagePaint.ColorSpace);
        int componentCount = colorSpace.Components;
        int bitsPerComponent = parameters.BitsPerComponent;
        bool isAlphaInterleaved = parameters.IsAlphaInterleaved;

        // The soft mask overrides the mask (ISO 32000-2, Table 87), so a color key applies only without one.
        int[]? colorKeyMask = null;

        if (!isAlphaInterleaved && alphaSource == null && parameters.ColorKeyMask?.Length == componentCount * 2)
        {
            colorKeyMask = parameters.ColorKeyMask;
        }

        bool hasPackedAlpha = isAlphaInterleaved || colorKeyMask != null;
        bool hasAlpha = hasPackedAlpha || alphaSource != null;
        int packedChannelCount = componentCount + (hasPackedAlpha ? 1 : 0);
        PdfRange[] decode = GetDecode(parameters, colorSpace, componentCount);
        bool isDirectColor = IsDirectColor(parameters, colorSpace, decode);
        bool usesLookup = !isDirectColor && UsesLookup(parameters, componentCount);

        List<IPdfImageRowProcessingStep> steps = [new PdfImageRowCropStep(bitsPerComponent, componentCount + (isAlphaInterleaved ? 1 : 0), sourceRegion)];

        if (colorKeyMask != null)
        {
            steps.Add(new PdfImageRowColorKeyMaskStep(sourceRegion.Width, componentCount, bitsPerComponent, colorKeyMask));
        }

        // Number of color channels the float rows carry ahead of their alpha channel.
        int floatColorCount;

        if (usesLookup)
        {
            PdfColorSampler sampler = CreateImageSampler(parameters.ImagePaint, decode);
            steps.Add(new PdfImageRowLookupStep(sampler, sourceRegion.Width, bitsPerComponent, RgbComponentCount, hasPackedAlpha));
            AddResize(steps, RgbComponentCount + (hasPackedAlpha ? 1 : 0), sourceRegion, outputRegion);
            floatColorCount = RgbComponentCount;
        }
        else
        {
            AddUnpackAndResize(steps, packedChannelCount, bitsPerComponent, sourceRegion, outputRegion);
            floatColorCount = componentCount;
        }

        if (alphaSource != null)
        {
            steps.Add(new PdfImageRowAlphaPlaneStep(alphaSource, outputRegion.Width, floatColorCount));
        }

        if (!isDirectColor && !usesLookup)
        {
            if (parameters.Matte != null && hasAlpha)
            {
                steps.Add(new PdfImageRowMatteStep(outputRegion.Width, NormalizeMatte(parameters.Matte, decode, componentCount)));
            }

            PdfColorSampler sampler = CreateImageSampler(parameters.ImagePaint, decode);
            steps.Add(new PdfImageRowColorStep(sampler, outputRegion.Width, componentCount, RgbComponentCount, hasAlpha));
            floatColorCount = RgbComponentCount;
        }

        int outputChannelCount = floatColorCount + (hasAlpha ? 1 : 0);
        steps.Add(new PdfImageRowFloatToByteStep(outputRegion.Width, outputChannelCount, ByteBitsPerComponent));

        PdfImageChannel[] channels;

        if (outputChannelCount == 1)
        {
            channels = GrayChannels;
        }
        else
        {
            if (outputChannelCount != RgbaChannels.Length)
            {
                steps.Add(new PdfImageRowExpandToRgbaStep(outputRegion.Width, outputChannelCount));
            }

            channels = RgbaChannels;
        }

        PdfImageSamples samples = new(outputRegion.Width, outputRegion.Height, PdfImageSampleFormat.Integer, ByteBitsPerComponent, channels);

        return new PdfImageRowProcessingPipeline(steps.ToArray(), samples);
    }

    private PdfColorSampler CreateImageSampler(PdfPaint imagePaint, PdfRange[] decode)
    {
        SkiaColorSampler colorSampler;

        if (_executionContext == null)
        {
            colorSampler = new SkiaColorSampler(imagePaint);
        }
        else
        {
            colorSampler = SkiaCommandUtilities.GetOrCreateColorSampler(_executionContext, imagePaint);
        }

        IPdfColorTransform[] paintTransforms = colorSampler.Sampler.Transforms;

        var transforms = new IPdfColorTransform[paintTransforms.Length + 1];
        transforms[0] = new PdfDecodeColorTransform(decode);
        paintTransforms.CopyTo(transforms, 1);

        return new PdfColorSampler(transforms);
    }

    private static PdfImageRowProcessingPipeline CreateStencilPipeline(
        PdfImageRowDecodingParameters parameters,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion)
    {
        int bitsPerComponent = parameters.BitsPerComponent;
        List<IPdfImageRowProcessingStep> steps = [new PdfImageRowCropStep(bitsPerComponent, 1, sourceRegion)];

        if (StencilPaintsOnZero(parameters))
        {
            steps.Add(new PdfImageRowInvertStep(sourceRegion.Width, 1, bitsPerComponent));
        }

        AddUnpackAndResize(steps, 1, bitsPerComponent, sourceRegion, outputRegion);
        steps.Add(new PdfImageRowFloatToByteStep(outputRegion.Width, 1, ByteBitsPerComponent));

        PdfImageSamples samples = new(outputRegion.Width, outputRegion.Height, PdfImageSampleFormat.Integer, ByteBitsPerComponent, AlphaChannels);

        return new PdfImageRowProcessingPipeline(steps.ToArray(), samples);
    }

    /// <summary>
    /// Whether the samples are DeviceGray or DeviceRGB components that reach the output unchanged.
    /// </summary>
    private static bool IsDirectColor(PdfImageRowDecodingParameters parameters, PdfColorSpaceConverter colorSpace, PdfRange[] decode)
    {
        bool isDeviceGrayOrRgb = colorSpace is PdfDeviceGrayColorSpaceConverter || colorSpace is PdfDeviceRgbColorSpaceConverter;

        return isDeviceGrayOrRgb
            && parameters.ImagePaint.TransferFunction == null
            && parameters.Matte == null
            && IsDefaultDecode(decode);
    }

    /// <summary>
    /// Whether every color of a single packed component is converted ahead through a table.
    /// </summary>
    private static bool UsesLookup(PdfImageRowDecodingParameters parameters, int componentCount)
    {
        int bitsPerComponent = parameters.BitsPerComponent;

        return componentCount == 1
            && parameters.Matte == null
            && (bitsPerComponent == 1 || bitsPerComponent == 2 || bitsPerComponent == 4 || bitsPerComponent == 8);
    }

    private static void AddUnpackAndResize(
        List<IPdfImageRowProcessingStep> steps,
        int channelCount,
        int bitsPerComponent,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion)
    {
        steps.Add(new PdfImageRowUnpackStep(sourceRegion.Width, channelCount, bitsPerComponent));
        AddResize(steps, channelCount, sourceRegion, outputRegion);
    }

    private static void AddResize(
        List<IPdfImageRowProcessingStep> steps,
        int channelCount,
        in PdfIntegerRectangle sourceRegion,
        in PdfIntegerRectangle outputRegion)
    {
        int sourceWidth = sourceRegion.Width;
        int sourceHeight = sourceRegion.Height;
        int outputWidth = outputRegion.Width;
        int outputHeight = outputRegion.Height;

        if (outputWidth > sourceWidth || outputHeight > sourceHeight)
        {
            steps.Add(new PdfImageRowNearestNeighborStep(channelCount, sourceWidth, outputWidth, sourceHeight, outputHeight));
        }
        else if (outputWidth < sourceWidth || outputHeight < sourceHeight)
        {
            steps.Add(new PdfImageRowBoxAverageStep(channelCount, sourceWidth, outputWidth, sourceHeight, outputHeight));
        }
    }

    /// <summary>
    /// The decode range of every component: the image's /Decode, or the default of ISO 32000-2 Table 88.
    /// </summary>
    private static PdfRange[] GetDecode(PdfImageRowDecodingParameters parameters, PdfColorSpaceConverter colorSpace, int componentCount)
    {
        PdfRange[]? decode = parameters.Decode;

        if (decode?.Length == componentCount)
        {
            return decode;
        }

        var defaultDecode = new PdfRange[componentCount];

        if (colorSpace is PdfIndexedColorSpaceConverter)
        {
            defaultDecode[0] = new PdfRange(0f, (1 << parameters.BitsPerComponent) - 1);
            return defaultDecode;
        }

        PdfRange[]? componentRanges = colorSpace.ComponentRanges;

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            if (componentRanges?.Length == componentCount)
            {
                defaultDecode[componentIndex] = componentRanges[componentIndex];
            }
            else
            {
                defaultDecode[componentIndex] = new PdfRange(0f, 1f);
            }
        }

        return defaultDecode;
    }

    private static bool IsDefaultDecode(PdfRange[] decode)
    {
        foreach (PdfRange range in decode)
        {
            if (range.Min != 0f || range.Max != 1f)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Maps the matte components onto the range 0 to 1 the samples take before decoding.
    /// </summary>
    private static float[] NormalizeMatte(float[] matte, PdfRange[] decode, int componentCount)
    {
        var normalizedMatte = new float[componentCount];

        for (int componentIndex = 0; componentIndex < componentCount; componentIndex++)
        {
            float matteComponent = (componentIndex < matte.Length) ? matte[componentIndex] : 0f;
            normalizedMatte[componentIndex] = decode[componentIndex].Normalize(matteComponent);
        }

        return normalizedMatte;
    }

    /// <summary>
    /// Whether a stencil mask paints where the sample is zero. A Decode of [1 0] swaps which sample value paints.
    /// </summary>
    private static bool StencilPaintsOnZero(PdfImageRowDecodingParameters parameters)
    {
        PdfRange[]? decode = parameters.Decode;

        return decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;
    }
}
