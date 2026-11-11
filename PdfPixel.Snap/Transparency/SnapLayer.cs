using PdfPixel.Color.ColorSpace;
using PdfPixel.Color.Paint;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Snap.Color;
using PdfPixel.Snap.Painting;
using PdfPixel.Transparency;
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.Snap.Transparency;

/// <summary>
/// A transparency group being painted (ISO 32000-2 11.4): its result is premultiplied float samples in its blending
/// space, kept with the group shape and, when it has a backdrop, the group alpha and initial backdrop. A non-isolated
/// group over a transparent backdrop is stored as isolated (11.4.5). The result is the caller's: disposing the layer
/// returns only its working planes.
/// </summary>
internal sealed class SnapLayer : IDisposable
{
    private const int ResultBitsPerComponent = 32;

    private readonly int _componentCount;
    private readonly int _pixelStride;
    private readonly SnapLayerPixels _pixels;
    private readonly SnapMemoryBlock? _groupAlpha;
    private readonly SnapMemoryBlock? _groupShape;
    private readonly SnapMemoryBlock? _backdrop;
    private readonly PdfIntegerRectangle _backdropBounds;
    private readonly SnapScratchBuffer _scratch;
    private SnapColorConverterCache _converters;
    private PdfIntegerRectangle _paintedBounds;
    private PdfIntegerRectangle _elementBounds;

    /// <summary>
    /// Opens <paramref name="group"/> over <paramref name="bounds"/>, compositing into <paramref name="result"/>, the
    /// caller's, as it holds it; a non-isolated group takes its initial backdrop from
    /// <paramref name="parent"/>; the group shape is stored only when <paramref name="keepsShape"/>.
    /// </summary>
    public SnapLayer(
        PdfImageSamples result,
        in PdfIntegerRectangle bounds,
        SnapLayerGroup group,
        SnapLayer? parent,
        SnapScratchBuffer scratch,
        SnapColorConverterCache converters,
        bool keepsShape)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        Bounds = GetLayerBounds(bounds);
        _scratch = scratch ?? throw new ArgumentNullException(nameof(scratch));
        _converters = converters ?? throw new ArgumentNullException(nameof(converters));

        Parent = parent;
        Knockout = group.Knockout;
        ColorSpace = group.ColorSpace;

        _componentCount = ColorSpace.Components;
        _pixelStride = _componentCount + 1;
        _pixels = new SnapLayerPixels(result, Bounds, _componentCount);
        Result = result;

        int pixelCount = Bounds.Width * Bounds.Height;
        _backdropBounds = PdfIntegerRectangle.Empty;

        if (keepsShape)
        {
            _groupShape = RentPlane(pixelCount);
        }

        if (!group.Isolated && parent != null)
        {
            _backdropBounds = GetOverlap(Bounds, parent.BackdropBounds);
        }

        Isolated = _backdropBounds.IsEmpty;

        if (!Isolated && parent != null)
        {
            _groupAlpha = RentPlane(pixelCount);
            _backdrop = RentPlane(pixelCount * _pixelStride);
            CopyInitialBackdrop(parent, _backdrop);
        }

        _paintedBounds = _backdropBounds;
        _elementBounds = PdfIntegerRectangle.Empty;
    }

    /// <summary>
    /// Creates the transparent result of a layer of <paramref name="group"/> over <paramref name="bounds"/> in
    /// <paramref name="block"/>, at least <see cref="GetResultLength"/> bytes long: premultiplied float samples of the
    /// group's components and alpha.
    /// </summary>
    public static PdfImageSamples CreateResult(in PdfIntegerRectangle bounds, SnapLayerGroup group, byte[] block)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        PdfIntegerRectangle layerBounds = GetLayerBounds(bounds);
        PdfImageChannel[] channels = SnapLayerPixels.CreateLayerChannels(group.ColorSpace.Components);
        PdfImageSamples result = new(block, layerBounds.Width, layerBounds.Height, PdfImageSampleFormat.Float, ResultBitsPerComponent, channels, isPremultiplied: true);
        ClearRows(result);

        return result;
    }

    /// <summary>
    /// Returns the number of bytes the result of a layer of <paramref name="group"/> over <paramref name="bounds"/>
    /// holds.
    /// </summary>
    public static int GetResultLength(in PdfIntegerRectangle bounds, SnapLayerGroup group)
    {
        if (group == null)
        {
            throw new ArgumentNullException(nameof(group));
        }

        PdfIntegerRectangle layerBounds = GetLayerBounds(bounds);

        return layerBounds.Width * layerBounds.Height * (group.ColorSpace.Components + 1) * sizeof(float);
    }

    public PdfIntegerRectangle Bounds { get; }

    /// <summary>
    /// Gets the layer the layer was opened in, or <see langword="null"/> for a root.
    /// </summary>
    public SnapLayer? Parent { get; }

    /// <summary>
    /// Gets the layer's result: premultiplied float samples of its components and alpha, the size of
    /// <see cref="Bounds"/>.
    /// </summary>
    public PdfImageSamples Result { get; }

    /// <summary>
    /// Gets the blending color space the layer composites in.
    /// </summary>
    public PdfColorSpaceConverter ColorSpace { get; }

    /// <summary>
    /// Gets whether the layer composites against a transparent initial backdrop.
    /// </summary>
    public bool Isolated { get; }

    /// <summary>
    /// Gets the pixels where a child group's backdrop may not be transparent: those painted so far or, in a knockout
    /// group, those of its own initial backdrop.
    /// </summary>
    public PdfIntegerRectangle BackdropBounds => Knockout ? _backdropBounds : _paintedBounds;

    /// <summary>
    /// Gets the pixels painted so far, initial backdrop included; every pixel outside is transparent.
    /// </summary>
    public PdfIntegerRectangle PaintedBounds => _paintedBounds;

    /// <summary>
    /// Gets the pixels the layer's own elements painted.
    /// </summary>
    public PdfIntegerRectangle ElementBounds => _elementBounds;

    public bool Knockout { get; }

    /// <summary>
    /// Gets whether the layer stores its group shape; a layer that does not is composited with shape 1, as an image.
    /// </summary>
    public bool KeepsShape => _groupShape != null;

    public SnapColorConverter GetConverter(PdfPaint paint) => _converters.GetConverter(paint, ColorSpace);

    /// <summary>
    /// Converts colors with <paramref name="converters"/> from now on.
    /// </summary>
    public void UpdateConverters(SnapColorConverterCache converters) => _converters = converters ?? throw new ArgumentNullException(nameof(converters));

    /// <summary>
    /// Takes what the result already holds as painted by the layer's own elements, over all of <see cref="Bounds"/>.
    /// </summary>
    public void MarkAllPainted()
    {
        _paintedBounds = Bounds;
        _elementBounds = Bounds;
    }

    /// <summary>
    /// Composites one element over a run of pixels inside <see cref="Bounds"/>.
    /// </summary>
    public void Compose(in SnapSourceRow row, PdfBlendMode blendMode)
    {
        int count = row.Count;
        int firstPixel = GetPixelIndex(row.Left, row.Y);
        using SnapLayerRun run = OpenComposedRun(row, blendMode);
        Span<float> results = run.Values;
        Span<float> groupShapes = GetGroupShapes(firstPixel, count);

        MarkPainted(row.Y, row.Left, count);

        if (!Knockout && SnapBlendFunctions.IsNormal(blendMode))
        {
            SnapSourceOver.ComposeElement(row, results, groupShapes, ColorSpace);

            if (_groupAlpha != null)
            {
                SnapGroupCompositing.AccumulateGroupAlphas(row, AsFloats(_groupAlpha, firstPixel, count));
            }
        }
        else
        {
            ComposeBlended(row, blendMode, firstPixel, results);

            if (!groupShapes.IsEmpty)
            {
                SnapGroupCompositing.AccumulateShapes(row, groupShapes);
            }
        }
    }

    /// <summary>
    /// Returns the converter for a closed <paramref name="group"/>'s colors into <see cref="ColorSpace"/> (11.7.2), or
    /// <see langword="null"/> when both composite in the same color space.
    /// </summary>
    public SnapColorConverter? GetGroupConverter(SnapLayer group, PdfPaint paint)
    {
        if (ReferenceEquals(group.ColorSpace, ColorSpace))
        {
            return null;
        }

        PdfPaint groupPaint = paint
            .WithColorSpace(group.ColorSpace, PdfDeviceColorMapping.None)
            .WithColorConversion(paint.RenderingIntent, paint.UseBlackPointCompensation, null);

        return GetConverter(groupPaint);
    }

    /// <summary>
    /// Composites a run of a closed group into this layer as one element (11.4.8), within the group's
    /// <see cref="ElementBounds"/>.
    /// </summary>
    public void ComposeGroupRow(
        int y,
        int left,
        int count,
        SnapLayer group,
        SnapColorConverter? converter,
        in SnapRowValues coverage,
        in SnapRowValues mask,
        PdfPaint paint)
    {
        PdfIntegerRectangle groupBounds = group.ElementBounds;
        int start = Math.Max(left, groupBounds.Left);
        int end = Math.Min(left + count, groupBounds.Right);

        if (y < groupBounds.Top || y >= groupBounds.Bottom || end <= start)
        {
            return;
        }

        int runCount = end - start;
        int offset = start - left;
        ReadOnlySpan<float> groupValues = group.ReadRow(y, start, runCount);
        SnapRowValues shapes = SnapRowValues.One;

        if (group.KeepsShape)
        {
            shapes = new SnapRowValues(group.ReadShapeRow(y, start, runCount), false);
        }

        SnapRowValues runCoverage = Slice(coverage, offset, runCount);
        SnapRowValues runMask = Slice(mask, offset, runCount);

        if (converter == null && !Knockout && SnapBlendFunctions.IsNormal(paint.BlendMode))
        {
            ComposePremultipliedGroupRow(y, start, runCount, groupValues, shapes, runCoverage, runMask, paint);
        }
        else
        {
            ComposeSplitGroupRow(y, start, runCount, group, converter, groupValues, shapes, runCoverage, runMask, paint);
        }
    }

    private void ComposePremultipliedGroupRow(
        int y,
        int start,
        int runCount,
        in ReadOnlySpan<float> groupValues,
        in SnapRowValues shapes,
        in SnapRowValues coverage,
        in SnapRowValues mask,
        PdfPaint paint)
    {
        int firstPixel = GetPixelIndex(start, y);
        using SnapLayerRun run = _pixels.Open(y, start, runCount);
        Span<float> results = run.Values;
        Span<float> groupShapes = GetGroupShapes(firstPixel, runCount);
        Span<float> groupAlphas = Span<float>.Empty;

        if (_groupAlpha != null)
        {
            groupAlphas = AsFloats(_groupAlpha, firstPixel, runCount);
        }

        SnapSourceRow row = new(y, start, runCount, SnapRowValues.One, coverage, shapes, SnapRowValues.One, mask, paint.Alpha, paint.AlphaIsShape);

        MarkPainted(y, start, runCount);
        SnapSourceOver.ComposeGroup(row, groupValues, results, groupShapes, groupAlphas, ColorSpace);
    }

    private void ComposeSplitGroupRow(
        int y,
        int start,
        int runCount,
        SnapLayer group,
        SnapColorConverter? converter,
        in ReadOnlySpan<float> groupValues,
        in SnapRowValues shapes,
        in SnapRowValues coverage,
        in SnapRowValues mask,
        PdfPaint paint)
    {
        int groupComponentCount = group.ColorSpace.Components;
        Span<float> sourceColors = _scratch.GetSourceColors(runCount * _componentCount);
        Span<float> sourceAlphas = _scratch.GetSourceAlphas(runCount);

        if (converter == null)
        {
            SnapGroupCompositing.SplitGroupResult(groupValues, sourceColors, sourceAlphas, _componentCount);
        }
        else
        {
            Span<float> groupColors = _scratch.GetGroupColors(runCount * groupComponentCount);
            SnapGroupCompositing.SplitGroupResult(groupValues, groupColors, sourceAlphas, groupComponentCount);
            ConvertGroupColors(converter, groupColors, groupComponentCount, sourceAlphas, sourceColors, runCount);
        }

        SnapRowValues colors = new(sourceColors, false);
        SnapRowValues alphas = new(sourceAlphas, false);
        SnapSourceRow row = new(y, start, runCount, colors, coverage, shapes, alphas, mask, paint.Alpha, paint.AlphaIsShape);

        Compose(row, paint.BlendMode);
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void ConvertGroupColors(
        SnapColorConverter converter,
        in Span<float> groupColors,
        int groupComponentCount,
        in ReadOnlySpan<float> alphas,
        in Span<float> colors,
        int count)
    {
        int componentCount = _componentCount;
        var colorByteCount = (uint)(componentCount * sizeof(float));
        ref float groupColor = ref MemoryMarshal.GetReference(groupColors);
        ref float alpha = ref MemoryMarshal.GetReference(alphas);
        ref float color = ref MemoryMarshal.GetReference(colors);
        ref float convertedGroupColor = ref groupColor;
        ref float convertedColor = ref color;
        var hasConvertedColor = false;

        for (int index = 0; index < count; index++)
        {
            if (alpha <= 0)
            {
                Unsafe.InitBlockUnaligned(ref Unsafe.As<float, byte>(ref color), 0, colorByteCount);
            }
            else if (hasConvertedColor && SnapComponents.AreEqual(ref groupColor, ref convertedGroupColor, groupComponentCount))
            {
                Unsafe.CopyBlockUnaligned(ref Unsafe.As<float, byte>(ref color), ref Unsafe.As<float, byte>(ref convertedColor), colorByteCount);
            }
            else
            {
                converter.Convert(ref groupColor, ref color);
                convertedGroupColor = ref groupColor;
                convertedColor = ref color;
                hasConvertedColor = true;
            }

            groupColor = ref Unsafe.Add(ref groupColor, groupComponentCount);
            alpha = ref Unsafe.Add(ref alpha, 1);
            color = ref Unsafe.Add(ref color, componentCount);
        }
    }

    /// <summary>
    /// Returns the layer's premultiplied result with the initial backdrop factored out, valid until the next read from
    /// a layer sharing the same scratch buffer.
    /// </summary>
    public ReadOnlySpan<float> ReadRow(int y, int left, int count)
    {
        int firstPixel = GetPixelIndex(left, y);
        ReadOnlySpan<float> results = _pixels.Read(y, left, count);

        if (_backdrop != null && _groupAlpha != null)
        {
            ReadOnlySpan<float> backdrops = AsFloats(_backdrop, firstPixel * _pixelStride, count * _pixelStride);
            Span<float> values = _scratch.GetGroupResults(count * _pixelStride);
            SnapGroupCompositing.FactorOutBackdrop(results, backdrops, AsFloats(_groupAlpha, firstPixel, count), values, _componentCount);

            return values;
        }
        else
        {
            return results;
        }
    }

    public ReadOnlySpan<float> ReadShapeRow(int y, int left, int count)
    {
        if (_groupShape == null)
        {
            throw new InvalidOperationException("The layer does not keep its group shape.");
        }

        return AsFloats(_groupShape, GetPixelIndex(left, y), count);
    }

    /// <summary>
    /// Reads the backdrop a child group starts from: what is painted so far or, in a knockout group, its own initial
    /// backdrop (11.4.6 NOTE 6); transparent outside the layer.
    /// </summary>
    public void ReadBackdropRow(int y, int left, in Span<float> values)
    {
        int start = Math.Max(left, Bounds.Left);
        int end = Math.Min(left + (values.Length / _pixelStride), Bounds.Right);

        if (y >= Bounds.Top && y < Bounds.Bottom && start < end)
        {
            int runStart = (start - left) * _pixelStride;
            int runEnd = (end - left) * _pixelStride;

            values.Slice(0, runStart).Clear();
            CopyBackdropRun(y, start, values.Slice(runStart, runEnd - runStart));
            values.Slice(runEnd).Clear();
        }
        else
        {
            values.Clear();
        }
    }

    private SnapLayerRun OpenComposedRun(in SnapSourceRow row, PdfBlendMode blendMode)
    {
        bool isOpaque = !Knockout
            && SnapBlendFunctions.IsNormal(blendMode)
            && row.ConstantAlpha == 1f
            && row.Coverage.IsOne
            && row.Alpha.IsOne
            && row.Mask.IsOne;

        if (isOpaque)
        {
            return _pixels.Overwrite(row.Y, row.Left, row.Count);
        }
        else
        {
            return _pixels.Open(row.Y, row.Left, row.Count);
        }
    }

    private void ComposeBlended(in SnapSourceRow row, PdfBlendMode blendMode, int firstPixel, in Span<float> results)
    {
        int count = row.Count;

        if (!Knockout)
        {
            SnapRowValues sourceTerms = GetSourceTerms(row, blendMode, results);
            SnapGroupCompositing.ComposeOverResult(row, sourceTerms, results, _componentCount);

            if (_groupAlpha != null)
            {
                SnapGroupCompositing.AccumulateGroupAlphas(row, AsFloats(_groupAlpha, firstPixel, count));
            }
        }
        else if (_backdrop == null || _groupAlpha == null)
        {
            SnapGroupCompositing.ComposeKnockoutIsolated(row, results, _componentCount);
        }
        else
        {
            ReadOnlySpan<float> backdrops = AsFloats(_backdrop, firstPixel * _pixelStride, count * _pixelStride);
            SnapRowValues sourceTerms = GetSourceTerms(row, blendMode, backdrops);
            SnapGroupCompositing.ComposeKnockout(row, sourceTerms, backdrops, results, AsFloats(_groupAlpha, firstPixel, count), _componentCount);
        }
    }

    private SnapRowValues GetSourceTerms(in SnapSourceRow row, PdfBlendMode blendMode, in ReadOnlySpan<float> backdrops)
    {
        if (SnapBlendFunctions.IsNormal(blendMode))
        {
            return row.Color;
        }
        else
        {
            int length = row.Count * _componentCount;
            Span<float> sourceTerms = _scratch.GetSourceTerms(length);
            Span<float> backdropColors = _scratch.GetBackdropColors(length);
            SnapGroupCompositing.ComputeSourceTerms(blendMode, row, backdrops, backdropColors, sourceTerms, ColorSpace);

            return new SnapRowValues(sourceTerms, false);
        }
    }

    private static SnapRowValues Slice(in SnapRowValues values, int offset, int count)
    {
        if (values.IsConstant)
        {
            return values;
        }
        else
        {
            return new SnapRowValues(values.Values.Slice(offset, count), false);
        }
    }

    private void CopyBackdropRun(int y, int left, in Span<float> values)
    {
        if (Knockout)
        {
            if (_backdrop != null)
            {
                AsFloats(_backdrop, GetPixelIndex(left, y) * _pixelStride, values.Length).CopyTo(values);
            }
        }
        else
        {
            _pixels.Read(y, left, values.Length / _pixelStride).CopyTo(values);
        }
    }

#if !NETSTANDARD2_0
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
#endif
    private void CopyInitialBackdrop(SnapLayer parent, SnapMemoryBlock backdrop)
    {
        int rowLength = _backdropBounds.Width * _pixelStride;

        for (int y = _backdropBounds.Top; y < _backdropBounds.Bottom; y++)
        {
            int rowStart = GetPixelIndex(_backdropBounds.Left, y) * _pixelStride;
            Span<float> backdrops = AsFloats(backdrop, rowStart, rowLength);

            parent.ReadBackdropRow(y, _backdropBounds.Left, backdrops);
            using SnapLayerRun run = _pixels.Open(y, _backdropBounds.Left, _backdropBounds.Width);
            backdrops.CopyTo(run.Values);
        }
    }

    private void MarkPainted(int y, int left, int count)
    {
        _paintedBounds = Widen(_paintedBounds, y, left, left + count);
        _elementBounds = Widen(_elementBounds, y, left, left + count);
    }

    /// <summary>
    /// Returns the group shapes of a run, empty when the layer does not keep its group shape.
    /// </summary>
    private Span<float> GetGroupShapes(int firstPixel, int count)
    {
        if (_groupShape == null)
        {
            return Span<float>.Empty;
        }
        else
        {
            return AsFloats(_groupShape, firstPixel, count);
        }
    }

    private static PdfIntegerRectangle Widen(in PdfIntegerRectangle bounds, int y, int left, int right)
    {
        if (bounds.IsEmpty)
        {
            return new PdfIntegerRectangle(left, y, right, y + 1);
        }
        else if (left < bounds.Left || right > bounds.Right || y < bounds.Top || y >= bounds.Bottom)
        {
            return new PdfIntegerRectangle(
                Math.Min(left, bounds.Left),
                Math.Min(y, bounds.Top),
                Math.Max(right, bounds.Right),
                Math.Max(y + 1, bounds.Bottom));
        }
        else
        {
            return bounds;
        }
    }

    private int GetPixelIndex(int x, int y) => ((y - Bounds.Top) * Bounds.Width) + (x - Bounds.Left);


    private static PdfIntegerRectangle GetOverlap(in PdfIntegerRectangle first, in PdfIntegerRectangle second)
    {
        PdfIntegerRectangle overlap = PdfIntegerRectangle.Intersect(first, second);

        if (overlap.Width > 0 && overlap.Height > 0)
        {
            return overlap;
        }
        else
        {
            return PdfIntegerRectangle.Empty;
        }
    }

    /// <summary>
    /// Rents a block of <paramref name="valueCount"/> cleared float values from the scratch buffer.
    /// </summary>
    private SnapMemoryBlock RentPlane(int valueCount)
    {
        SnapMemoryBlock block = _scratch.MemoryPool.Rent(valueCount * sizeof(float));
        AsFloats(block, 0, valueCount).Clear();

        return block;
    }

    private static Span<float> AsFloats(SnapMemoryBlock block, int firstValue, int valueCount)
        => MemoryMarshal.Cast<byte, float>(block.Array.AsSpan()).Slice(firstValue, valueCount);


    private static void ClearRows(PdfImageSamples samples)
    {
        for (int row = 0; row < samples.Height; row++)
        {
            samples.GetRow(row).Clear();
        }
    }

    /// <summary>
    /// Returns the bounds a layer opened over <paramref name="bounds"/> covers: empty when they hold no pixel.
    /// </summary>
    private static PdfIntegerRectangle GetLayerBounds(in PdfIntegerRectangle bounds)
    {
        if (bounds.Width > 0 && bounds.Height > 0)
        {
            return bounds;
        }
        else
        {
            return PdfIntegerRectangle.Empty;
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _backdrop?.Dispose();
        _groupAlpha?.Dispose();
        _groupShape?.Dispose();
    }
}
