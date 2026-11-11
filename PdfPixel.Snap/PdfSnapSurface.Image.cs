using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Icc;
using PdfPixel.Color.Paint;
using PdfPixel.Color.Transform;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging;
using PdfPixel.Imaging.Decoding;
using PdfPixel.Imaging.Processing;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Objects;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Imaging;
using PdfPixel.Snap.Painting;
using PdfPixel.Snap.Rasterization;
using System;
using System.Collections.Generic;

namespace PdfPixel.Snap;

public sealed partial class PdfSnapSurface
{
    private const int FloatBitsPerComponent = sizeof(float) * 8;

    private static readonly PdfRange[] UnitDecode = [new PdfRange(0f, 1f)];

    // The image occupies the unit square of user space with its first row at the top.
    private static readonly PdfRectangle ImageFootprint = new(0, 0, 1, 1);

    // Maps the unit square, first row at the top, onto PDF image space, whose first row is at y = 1.
    private static readonly PdfMatrix ImageMatrix = PdfMatrix.Concat(PdfMatrix.CreateScale(1, -1), PdfMatrix.CreateTranslation(0, -1));

    /// <summary>
    /// Draws <paramref name="samples"/> over the unit square, its first row at the top, averaging samples where a pixel
    /// covers more than one; an alpha channel is the samples' soft mask.
    /// </summary>
    /// <param name="samples">
    /// Integer samples holding each component of the paint's color space once, in any order, and at most one alpha
    /// channel.
    /// </param>
    /// <param name="matrix">The matrix from the unit square to current user space.</param>
    /// <param name="paint">The paint whose color space the samples are in, composited with its alpha and blend mode.</param>
    /// <param name="interpolate">Whether the samples are interpolated where they are enlarged.</param>
    public void DrawImage(PdfImageSamples samples, in PdfMatrix matrix, PdfPaint paint, bool interpolate)
    {
        PdfImageSamplesDecoder decoder = new(samples, paint, interpolate);

        DrawImage(decoder, paint, matrix);
    }

    /// <summary>
    /// Draws the samples of <paramref name="decoder"/> over the unit square, its first row at the top, averaging samples
    /// where a pixel covers more than one.
    /// </summary>
    /// <param name="decoder">The decoder of the image, kept across draws to reuse its tiles.</param>
    /// <param name="paint">The paint the image is composited with.</param>
    /// <param name="matrix">The matrix from the unit square to current user space.</param>
    public void DrawImage(IPdfImageDecoder decoder, PdfPaint paint, in PdfMatrix matrix)
    {
        if (decoder == null)
        {
            throw new ArgumentNullException(nameof(decoder));
        }

        if (paint == null)
        {
            throw new ArgumentNullException(nameof(paint));
        }

        DrawImage(decoder, maskDecoder: null, paint, source: null, matrix);
    }

    /// <summary>
    /// Draws <paramref name="image"/> with <paramref name="paint"/>, masked by the image its <see cref="PdfImage.MaskKind"/>
    /// names, over the unit square of user space.
    /// </summary>
    private void DrawImage(PdfImage image, PdfPaint paint, PdfGroupSource? source)
    {
        IPdfImageDecoder? decoder = image.Decoder;

        if (decoder == null)
        {
            ReportUnsupported(PdfCommandKind.DrawImage);
            return;
        }

        DrawImage(decoder, GetMaskImage(image)?.Decoder, paint, source, ImageMatrix);
    }

    /// <summary>
    /// Draws an image by the image rule of ISO 32000-2 10.7.4, averaging samples where a pixel covers more than one,
    /// over the unit square <paramref name="imageMatrix"/> maps to user space.
    /// </summary>
    private void DrawImage(IPdfImageDecoder decoder, IPdfImageDecoder? maskDecoder, PdfPaint paint, PdfGroupSource? source, in PdfMatrix imageMatrix)
    {
        if (IsEmpty)
        {
            return;
        }

        PrepareTextObjectDraw(paint);

        PdfMatrix unitToDevice = _state.DeviceMatrix.PreConcat(imageMatrix);
        PdfRectangle footprint = ImageFootprint;
        PdfIntegerRectangle bounds = PdfIntegerRectangle.Intersect(PdfSnapGrid.RoundOut(unitToDevice.MapRect(footprint)), GetDrawLimits());

        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        SnapImageArea area = GetImageArea(unitToDevice, footprint, bounds);

        if (area.Visible.Width <= 0 || area.Visible.Height <= 0)
        {
            return;
        }

        SnapImageGeometry geometry = new(this, area.Footprint);

        try
        {
            if (decoder.HasImageMask)
            {
                using SnapImageStencil? stencil = CreateImageStencil(decoder, area);

                if (stencil != null)
                {
                    DrawWithPaint(new SnapImageStencilGeometry(this, stencil), bounds, paint, source);
                }
            }
            else
            {
                DrawColorImage(decoder, maskDecoder, paint, geometry, area);
            }
        }
        finally
        {
            decoder.ReleaseTiles();
            maskDecoder?.ReleaseTiles();
        }
    }

    /// <summary>
    /// Returns the image <paramref name="image"/> is masked by: its soft mask or its stencil mask, or
    /// <see langword="null"/> when no image masks it.
    /// </summary>
    private static PdfImage? GetMaskImage(PdfImage image)
    {
        switch (image.MaskKind)
        {
            case PdfImageMaskKind.SoftMask:
            {
                return image.SoftMask;
            }
            case PdfImageMaskKind.Mask:
            {
                return image.Mask;
            }
            default:
            {
                return null;
            }
        }
    }

    /// <summary>
    /// Draws a color image over its footprint, or over its explicit stencil mask as its shape (ISO 32000-2 11.6.4.2).
    /// </summary>
    private void DrawColorImage(IPdfImageDecoder decoder, IPdfImageDecoder? maskDecoder, PdfPaint paint, in SnapImageGeometry geometry, in SnapImageArea area)
    {
        using SnapImagePainter? painter = CreateImagePainter(decoder, maskDecoder, paint, area);

        if (painter == null)
        {
            return;
        }

        if (decoder.MaskKind == PdfImageMaskKind.Mask && maskDecoder != null)
        {
            DrawColorImageShape(maskDecoder, area, painter);
        }
        else
        {
            geometry.Draw(painter);
        }
    }

    /// <summary>
    /// Draws a color image over its explicit stencil mask as its shape (ISO 32000-2 11.6.4.2).
    /// </summary>
    private void DrawColorImageShape(IPdfImageDecoder maskDecoder, in SnapImageArea area, SnapImagePainter painter)
    {
        using SnapImageStencil? shape = CreateImageStencil(maskDecoder, area);

        if (shape == null)
        {
            return;
        }

        new SnapImageStencilGeometry(this, shape).Draw(painter);
    }

    /// <summary>
    /// Returns the painter of a color image, its soft mask or opacity channel as mask (ISO 32000-2 11.6.4.3) and color
    /// key as shape (11.6.4.2), or <see langword="null"/> when it cannot be drawn.
    /// </summary>
    private SnapImagePainter? CreateImagePainter(IPdfImageDecoder decoder, IPdfImageDecoder? maskDecoder, PdfPaint paint, in SnapImageArea area)
    {
        PdfImageMaskKind maskKind = decoder.MaskKind;
        PdfImageDecodeInfo decodeInfo = SelectImageTiles(decoder, area);
        PdfImageSampleInfo samples = decodeInfo.Samples;
        PdfPaint samplesPaint = paint.WithColorSpace(samples.ColorSpace, paint.DeviceColorMapping);
        PdfColorSpaceConverter colorSpace = samplesPaint.DeviceColorMapping.Map(samplesPaint.ColorSpace);
        PdfRange[] decode = GetColorDecode(decoder.Decode, colorSpace, samples.ComponentCount, samples.BitsPerComponent);
        PdfPalette? palette = null;
        PdfPaint imagePaint = samplesPaint;

        if (colorSpace is PdfIndexedColorSpaceConverter indexed)
        {
            int bitsPerComponent = samples.BitsPerComponent;

            if (bitsPerComponent != 1 && bitsPerComponent != 2 && bitsPerComponent != 4 && bitsPerComponent != 8)
            {
                ReportUnsupported(PdfCommandKind.DrawImage);
                return null;
            }

            palette = indexed.Palette;
            imagePaint = paint.WithColorSpace(indexed.BaseColorSpace, paint.DeviceColorMapping);
        }

        int[]? colorKey = null;

        if (maskKind == PdfImageMaskKind.ColorKey && decoder.ColorKeyMask?.Length == samples.ComponentCount * 2)
        {
            colorKey = decoder.ColorKeyMask;
        }

        SnapImageSourceSteps colorSteps = SnapImageSourceSteps.ForColors(samples, decode, colorKey, palette);
        SnapImagePlacement colors = CreatePlacement(decoder, readsAlpha: false, invertsSamples: false, samples, colorSteps, area);

        try
        {
            return CreateMaskedImagePainter(decoder, maskDecoder, paint, area, decodeInfo, decode, colorSteps, colors, palette, imagePaint);
        }
        catch
        {
            colors.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Returns the painter of a color image whose colors are placed by <paramref name="colors"/>, creating the
    /// placement of its mask.
    /// </summary>
    private SnapImagePainter CreateMaskedImagePainter(
        IPdfImageDecoder decoder,
        IPdfImageDecoder? maskDecoder,
        PdfPaint paint,
        in SnapImageArea area,
        PdfImageDecodeInfo decodeInfo,
        PdfRange[] decode,
        SnapImageSourceSteps colorSteps,
        SnapImagePlacement colors,
        PdfPalette? palette,
        PdfPaint imagePaint)
    {
        PdfImageMaskKind maskKind = decoder.MaskKind;
        SnapImagePlacement? mask = null;
        float[]? matte = null;

        switch (maskKind)
        {
            case PdfImageMaskKind.SoftMask:
            {
                if (maskDecoder != null)
                {
                    PdfImageSampleInfo maskSamples = SelectImageTiles(maskDecoder, area).Samples;
                    SnapImageSourceSteps maskSteps = SnapImageSourceSteps.ForMask(maskSamples, GetMaskDecode(maskDecoder.Decode));

                    mask = CreatePlacement(maskDecoder, readsAlpha: false, invertsSamples: false, maskSamples, maskSteps, area);
                    matte = decoder.Matte;
                }

                break;
            }
            case PdfImageMaskKind.SoftMaskInData:
            case PdfImageMaskKind.PremultipliedSoftMaskInData:
            {
                PdfImageSampleInfo? alphaSamples = decodeInfo.Alpha;

                if (alphaSamples != null)
                {
                    mask = CreatePlacement(decoder, readsAlpha: true, invertsSamples: false, alphaSamples, SnapImageSourceSteps.ForMask(alphaSamples, UnitDecode), area);

                    if (maskKind == PdfImageMaskKind.PremultipliedSoftMaskInData && palette == null)
                    {
                        matte = GetDecodedZero(decode);
                    }
                }

                break;
            }
        }

        if (mask == null || matte?.Length != colorSteps.ComponentCount)
        {
            matte = null;
        }

        SnapColorConverter converter = _state.Target.GetConverter(imagePaint);
        float paintAlpha = ResolveColor(paint, _groupColor);

        return new SnapImagePainter(
            this,
            colors,
            colorSteps.ComponentCount,
            mask,
            matte,
            converter,
            paint,
            _groupColor,
            paintAlpha,
            _state.TintPaint != null,
            area.Visible.Width);
    }

    /// <summary>
    /// Returns where an image lands, by one rule for color images and stencil masks: the whole pixels of an image along
    /// the device axes, quarter turns included, and the outline by pixel centers of any other.
    /// </summary>
    private SnapImageArea GetImageArea(in PdfMatrix unitToDevice, in PdfRectangle footprint, in PdfIntegerRectangle bounds)
    {
        PdfIntegerRectangle? pixels = PdfSnapGrid.GetImagePixels(footprint, unitToDevice);
        SnapImageFootprint imageFootprint = new(pixels, footprint, unitToDevice);

        if (pixels != null && PdfSnapGrid.IsUnturned(unitToDevice))
        {
            return new SnapImageArea(unitToDevice, PdfIntegerRectangle.Intersect(pixels.Value, GetDrawLimits()), isAligned: true, imageFootprint);
        }
        else
        {
            return new SnapImageArea(unitToDevice, bounds, isAligned: false, imageFootprint);
        }
    }

    /// <summary>
    /// Selects the decoder's tiles for the image's whole footprint, prefetching what the visible region reaches.
    /// </summary>
    private PdfImageDecodeInfo SelectImageTiles(IPdfImageDecoder decoder, in SnapImageArea area)
    {
        PdfMatrix unitToDevice = area.UnitToDevice;
        PdfIntegerRectangle? alignedPixels = null;
        PdfIntegerSize targetSize;
        PdfRectangle? region = null;

        if (area.IsAligned)
        {
            alignedPixels = area.Footprint.Pixels;
        }

        if (alignedPixels != null)
        {
            targetSize = new PdfIntegerSize(alignedPixels.Value.Width, alignedPixels.Value.Height);
        }
        else
        {
            targetSize = PdfSnapGrid.GetResizedSize(unitToDevice);
        }

        if (_state.VisibleRegion != null)
        {
            region = unitToDevice.Invert().MapRect(_state.VisibleRegion.Value);
        }

        return decoder.SelectTiles(targetSize, region, _imageCache, _executionContext.ExecutionObserver);
    }

    private SnapImagePlacement CreatePlacement(
        IPdfImageDecoder decoder,
        bool readsAlpha,
        bool invertsSamples,
        PdfImageSampleInfo samples,
        SnapImageSourceSteps sourceSteps,
        in SnapImageArea area)
    {
        PdfIntegerSize sampleSize = samples.Grid.ImageSize;
        PdfMatrix unitToDevice = area.UnitToDevice;
        PdfIntegerRectangle visible = area.Visible;

        if (area.IsAligned)
        {
            PdfImageRowAxisMapping horizontal = PdfSnapGrid.GetImageColumnMapping(sampleSize.Width, visible, unitToDevice);
            PdfImageRowAxisMapping vertical = PdfSnapGrid.GetImageRowMapping(sampleSize.Height, visible, unitToDevice);
            bool mirrors = unitToDevice.ScaleX < 0;
            bool readsBottomUp = unitToDevice.ScaleY < 0;
            SnapImageRowSource rowSource = CreateRowSource(decoder, readsAlpha, invertsSamples, samples, sourceSteps, horizontal, vertical, readsBottomUp, mirrors, keptRowCount: 1);

            return new SnapAlignedImagePlacement(rowSource, visible);
        }
        else
        {
            SnapResizedImageGrid grid = PdfSnapGrid.GetResizedImageGrid(sampleSize, visible, unitToDevice);
            int bandRowCount = GetBandRowCount(grid.DeviceToResized, visible.Width, grid.Vertical.DestinationCount);
            SnapImageRowSource rowSource = CreateRowSource(decoder, readsAlpha, invertsSamples, samples, sourceSteps, grid.Horizontal, grid.Vertical, grid.ReadsBottomUp, mirrors: false, bandRowCount);

            return new SnapTransformedImagePlacement(rowSource, grid.DeviceToResized, visible, grid.Horizontal.DestinationCount, grid.Vertical.DestinationCount);
        }
    }

    /// <summary>
    /// Returns how many resized rows a device row <paramref name="visibleWidth"/> pixels wide can cross under
    /// <paramref name="deviceToResized"/>, at most <paramref name="resizedHeight"/>.
    /// </summary>
    private static int GetBandRowCount(in PdfMatrix deviceToResized, int visibleWidth, int resizedHeight)
    {
        int crossedRows = (int)Math.Ceiling(Math.Abs(deviceToResized.SkewY) * visibleWidth) + 2;

        return Math.Max(1, Math.Min(resizedHeight, crossedRows));
    }

    private SnapImageRowSource CreateRowSource(
        IPdfImageDecoder decoder,
        bool readsAlpha,
        bool invertsSamples,
        PdfImageSampleInfo samples,
        SnapImageSourceSteps sourceSteps,
        in PdfImageRowAxisMapping horizontal,
        in PdfImageRowAxisMapping vertical,
        bool readsBottomUp,
        bool mirrors,
        int keptRowCount)
    {
        int channelCount = sourceSteps.ChannelCount;
        PdfImageRowInterpolationMode? interpolation = null;

        if (horizontal.Grows || vertical.Grows)
        {
            interpolation = GetInterpolationMode(decoder, sourceSteps);
        }

        (IPdfImageRowProcessingStep[] resizeSteps, PdfIntegerRectangle window) = CreateResize(sourceSteps, horizontal, vertical, interpolation);
        SnapImageReader reader = new(decoder, readsAlpha, invertsSamples, samples, _executionContext.ExecutionObserver, window.Left, window.Width, Scratch.MemoryPool);
        IPdfImageRowProcessingStep[] steps = sourceSteps.Create(window.Width, resizeSteps, mirrors);

        return new SnapImageRowSource(
            reader,
            window.Top,
            window.Height,
            samples.Grid.ImageSize.Height,
            readsBottomUp,
            steps,
            channelCount,
            horizontal.DestinationCount,
            keptRowCount,
            Scratch.MemoryPool);
    }

    /// <summary>
    /// Returns how a source is interpolated where it is enlarged, by its <see cref="SnapImageInterpolation"/>, or
    /// <see langword="null"/> when it is not.
    /// </summary>
    private static PdfImageRowInterpolationMode? GetInterpolationMode(IPdfImageDecoder decoder, SnapImageSourceSteps sourceSteps)
    {
        if (sourceSteps.Interpolation == SnapImageInterpolation.ByArea)
        {
            return PdfImageRowInterpolationMode.Area;
        }
        else if (sourceSteps.Interpolation == SnapImageInterpolation.AsRequested && decoder.Interpolate)
        {
            return PdfImageRowInterpolationMode.Linear;
        }
        else
        {
            return null;
        }
    }

    /// <summary>
    /// Returns the steps resizing to device scale, on packed codes or on floats after a palette or color key, and the
    /// source window they read: the box resize, followed when there is an <paramref name="interpolation"/> by the
    /// interpolation of the axes that grow, which the box resize then keeps at their source samples. Where both axes
    /// map one source sample to one destination sample there is nothing to resize: packed codes are only unpacked.
    /// </summary>
    private static (IPdfImageRowProcessingStep[] Steps, PdfIntegerRectangle Window) CreateResize(
        SnapImageSourceSteps sourceSteps,
        in PdfImageRowAxisMapping horizontal,
        in PdfImageRowAxisMapping vertical,
        PdfImageRowInterpolationMode? interpolation)
    {
        if (interpolation == null && horizontal.IsOneToOne && vertical.IsOneToOne)
        {
            int left = horizontal.GetNearestSource(0);
            int top = vertical.GetNearestSource(0);
            PdfIntegerRectangle window = new(left, top, left + horizontal.DestinationCount, top + vertical.DestinationCount);

            if (sourceSteps.AveragesCodes)
            {
                return ([new PdfImageRowUnpackStep(window.Width, sourceSteps.ChannelCount, sourceSteps.BitsPerComponent)], window);
            }
            else
            {
                return ([], window);
            }
        }
        else if (interpolation != null)
        {
            PdfImageRowAxisMapping boxHorizontal = GetBoxMapping(horizontal);
            PdfImageRowAxisMapping boxVertical = GetBoxMapping(vertical);
            PdfImageRowResizeStep box = CreateBoxResize(sourceSteps, boxHorizontal, boxVertical);
            PdfImageRowInterpolationStep interpolationStep = new(sourceSteps.ChannelCount, interpolation.Value, horizontal, vertical);

            return ([box, interpolationStep], box.SourceWindow);
        }
        else
        {
            PdfImageRowResizeStep box = CreateBoxResize(sourceSteps, horizontal, vertical);

            return ([box], box.SourceWindow);
        }
    }

    /// <summary>
    /// Returns the mapping the box resize uses along an axis that is interpolated after it: the source samples the
    /// interpolation reads where the axis grows, otherwise the axis as it is.
    /// </summary>
    private static PdfImageRowAxisMapping GetBoxMapping(in PdfImageRowAxisMapping mapping)
    {
        if (mapping.Grows)
        {
            return mapping.GetSourceSamplesMapping(mapping.GetInterpolationRange());
        }
        else
        {
            return mapping;
        }
    }

    private static PdfImageRowResizeStep CreateBoxResize(
        SnapImageSourceSteps sourceSteps,
        in PdfImageRowAxisMapping horizontal,
        in PdfImageRowAxisMapping vertical)
    {
        if (sourceSteps.AveragesCodes)
        {
            return new PdfImageRowResizeStep(sourceSteps.ChannelCount, PdfImageSampleFormat.Integer, sourceSteps.BitsPerComponent, horizontal, vertical);
        }
        else
        {
            return new PdfImageRowResizeStep(sourceSteps.ChannelCount, PdfImageSampleFormat.Float, FloatBitsPerComponent, horizontal, vertical);
        }
    }

    /// <summary>
    /// Returns the image's /Decode, or the default of ISO 32000-2 Table 88.
    /// </summary>
    private static PdfRange[] GetColorDecode(PdfRange[]? decode, PdfColorSpaceConverter colorSpace, int componentCount, int bitsPerComponent)
    {
        if (decode?.Length == componentCount)
        {
            return decode;
        }

        var defaultDecode = new PdfRange[componentCount];

        if (colorSpace is PdfIndexedColorSpaceConverter)
        {
            defaultDecode[0] = new PdfRange(0f, (1 << bitsPerComponent) - 1);
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

    private static PdfRange[] GetMaskDecode(PdfRange[]? decode)
    {
        if (decode?.Length == 1)
        {
            return decode;
        }

        return UnitDecode;
    }

    /// <summary>
    /// Returns whether a stencil mask paints where the sample is zero (ISO 32000-2 8.9.6.2); a Decode of [1 0] swaps it.
    /// </summary>
    private static bool StencilPaintsOnZero(PdfRange[]? decode) => decode == null || decode.Length < 1 || decode[0].Min < decode[0].Max;

    /// <summary>
    /// Returns what each component's code 0 decodes to: the matte of colors premultiplied with an opacity channel.
    /// </summary>
    private static float[] GetDecodedZero(PdfRange[] decode)
    {
        var values = new float[decode.Length];

        for (int component = 0; component < decode.Length; component++)
        {
            values[component] = decode[component].Min;
        }

        return values;
    }

    private readonly struct SnapImageArea
    {
        public SnapImageArea(in PdfMatrix unitToDevice, in PdfIntegerRectangle visible, bool isAligned, in SnapImageFootprint footprint)
        {
            UnitToDevice = unitToDevice;
            Visible = visible;
            IsAligned = isAligned;
            Footprint = footprint;
        }

        public PdfMatrix UnitToDevice { get; }

        public PdfIntegerRectangle Visible { get; }

        public bool IsAligned { get; }

        public SnapImageFootprint Footprint { get; }
    }

    /// <summary>
    /// The row-processing steps of one image source around its resize.
    /// </summary>
    private sealed class SnapImageSourceSteps
    {
        private readonly PdfImageSampleInfo _samples;
        private readonly PdfRange[]? _decode;
        private readonly int[]? _colorKey;
        private readonly PdfPalette? _palette;

        private SnapImageSourceSteps(
            PdfImageSampleInfo samples,
            PdfRange[]? decode,
            int[]? colorKey,
            PdfPalette? palette,
            int componentCount,
            SnapImageInterpolation interpolation)
        {
            _samples = samples;
            _decode = decode;
            _colorKey = colorKey;
            _palette = palette;
            ComponentCount = componentCount;
            Interpolation = interpolation;
        }

        public int ComponentCount { get; }

        /// <summary>
        /// Gets how the source is interpolated where it is enlarged.
        /// </summary>
        public SnapImageInterpolation Interpolation { get; }

        /// <summary>
        /// Gets the components, plus the color key's shape when there is one.
        /// </summary>
        public int ChannelCount => ComponentCount + ((_colorKey != null) ? 1 : 0);

        public static SnapImageSourceSteps ForColors(PdfImageSampleInfo samples, PdfRange[] decode, int[]? colorKey, PdfPalette? palette)
        {
            int componentCount = (palette != null) ? palette.BaseColorSpace.Components : samples.ComponentCount;
            var interpolation = SnapImageInterpolation.AsRequested;

            if (samples.BitsPerComponent == 1)
            {
                interpolation = SnapImageInterpolation.ByArea;
            }

            return new SnapImageSourceSteps(samples, decode, colorKey, palette, componentCount, interpolation);
        }

        public static SnapImageSourceSteps ForMask(PdfImageSampleInfo samples, PdfRange[] decode)
            => new(samples, decode, null, null, 1, SnapImageInterpolation.AsRequested);

        public static SnapImageSourceSteps ForStencil(PdfImageSampleInfo samples)
            => new(samples, null, null, null, 1, SnapImageInterpolation.Never);

        /// <summary>
        /// Gets whether the source, without palette or color key, is resized by averaging its packed codes before
        /// Decode.
        /// </summary>
        public bool AveragesCodes => _palette == null && _colorKey == null;

        public int BitsPerComponent => _samples.BitsPerComponent;

        public IPdfImageRowProcessingStep[] Create(int width, IPdfImageRowProcessingStep[] resizeSteps, bool mirrors)
        {
            List<IPdfImageRowProcessingStep> steps = [];

            if (AveragesCodes)
            {
                steps.AddRange(resizeSteps);

                if (_decode != null && !IsUnitDecode(_decode))
                {
                    steps.Add(new PdfImageRowDecodeStep(_decode, ChannelCount));
                }
            }
            else
            {
                AddUnpackedSteps(steps, width, resizeSteps);
            }

            if (mirrors)
            {
                steps.Add(new PdfImageRowMirrorStep(ChannelCount));
            }

            return steps.ToArray();
        }

        /// <summary>
        /// Returns whether every range of <paramref name="decode"/> is 0 to 1, so that it maps samples onto themselves.
        /// </summary>
        private static bool IsUnitDecode(PdfRange[] decode)
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
        /// Adds the color key on raw codes, unpack and Decode or the palette lookup, and premultiplies by the key around
        /// the resize.
        /// </summary>
        private void AddUnpackedSteps(List<IPdfImageRowProcessingStep> steps, int width, IPdfImageRowProcessingStep[] resizeSteps)
        {
            int bitsPerComponent = _samples.BitsPerComponent;
            int componentCount = _samples.ComponentCount;
            bool hasColorKey = _colorKey != null;

            if (_colorKey != null)
            {
                steps.Add(new PdfImageRowColorKeyMaskStep(width, componentCount, bitsPerComponent, _colorKey));
            }

            if (_palette != null && _decode != null)
            {
                steps.Add(new PdfImageRowLookupStep(CreatePaletteSampler(_palette, _decode), width, bitsPerComponent, ComponentCount, hasColorKey));
            }
            else
            {
                steps.Add(new PdfImageRowUnpackStep(width, ChannelCount, bitsPerComponent));

                if (_decode != null && !IsUnitDecode(_decode))
                {
                    steps.Add(new PdfImageRowDecodeStep(_decode, ChannelCount));
                }
            }

            if (hasColorKey)
            {
                steps.Add(new PdfImageRowPremultiplyStep(ComponentCount, PdfImageSampleFormat.Float, FloatBitsPerComponent));
            }

            steps.AddRange(resizeSteps);

            if (hasColorKey)
            {
                steps.Add(new PdfImageRowUnpremultiplyStep(ComponentCount, PdfImageSampleFormat.Float, FloatBitsPerComponent));
            }
        }

        private static PdfColorSampler CreatePaletteSampler(PdfPalette palette, PdfRange[] decode)
        {
            int baseComponentCount = palette.BaseColorSpace.Components;
            PdfChainedColorTransform baseIdentity = new(new ChainedColorTransform(), baseComponentCount, baseComponentCount);
            IPdfColorTransform[] transforms = [new PdfDecodeColorTransform(decode), new PdfPaletteColorTransform(palette, baseIdentity)];

            return new PdfColorSampler(transforms);
        }
    }

    private readonly struct SnapImageGeometry : ISnapGeometry
    {
        private readonly PdfSnapSurface _surface;
        private readonly SnapImageFootprint _footprint;

        public SnapImageGeometry(PdfSnapSurface surface, in SnapImageFootprint footprint)
        {
            _surface = surface;
            _footprint = footprint;
        }

        /// <inheritdoc />
        public void Draw<TPainter>(TPainter painter)
            where TPainter : ISnapRowPainter
        {
            if (_footprint.Pixels == null)
            {
                _footprint.AddOutline(_surface.Scratch.Edges);
            }

            _surface.Draw(_footprint.Pixels, PdfPathFillType.Winding, areaCoverage: false, painter);
        }
    }
}
