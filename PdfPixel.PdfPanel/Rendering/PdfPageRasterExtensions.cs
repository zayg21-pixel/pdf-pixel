using PdfPixel.Color.ColorSpace;
using PdfPixel.Commands;
using PdfPixel.Geometry;
using PdfPixel.Imaging.Processing.Pipeline;
using PdfPixel.Transparency;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfPixel.PdfPanel.Rendering;

/// <summary>
/// Rasterizes pages onto transparent premultiplied 8-bit RGBA samples blending in DeviceRGB and puts them on white
/// paper.
/// </summary>
internal static class PdfPageRasterExtensions
{
    /// <summary>
    /// The bytes of one pixel of the samples pages are rasterized onto.
    /// </summary>
    public const int RasterBytesPerPixel = 4;

    private const int BitsPerComponent = 8;
    private const int RgbBytesPerPixel = 3;

    // A pixel read as a little-endian 32-bit value holds red in its lowest byte and alpha in its highest.
    private const int AlphaShift = 24;
    private const uint OpaqueValue = 255;
    private const uint OpaqueAlpha = 0xFF000000u;
    private const uint ComponentSpread = 0x00010101u;

    private static readonly PdfImageChannel[] RgbaChannels = [PdfImageChannel.Component(0), PdfImageChannel.Component(1), PdfImageChannel.Component(2), PdfImageChannel.Alpha];

    /// <summary>
    /// Creates premultiplied 8-bit RGBA samples of <paramref name="width"/> by <paramref name="height"/> pixels over
    /// <paramref name="buffer"/>, at least <paramref name="width"/> × <paramref name="height"/> × 4 bytes long.
    /// </summary>
    public static PdfImageSamples CreateRasterSamples(byte[] buffer, int width, int height)
        => new(buffer, width, height, PdfImageSampleFormat.Integer, BitsPerComponent, RgbaChannels, isPremultiplied: true);

    /// <summary>
    /// Returns the command that draws <paramref name="pageGroup"/> onto an isolated, transparent root blending in
    /// DeviceRGB: the group's recording itself when the group declares DeviceRGB or no color space and has no knockout,
    /// since the root then composites exactly as the group would; otherwise the group.
    /// </summary>
    public static IPdfCommand GetRootContent(this DrawGroupCommand pageGroup)
    {
        PdfTransparencyGroupContext group = pageGroup.Source.Group;
        bool blendsLikeRoot = group.ColorSpace == null
            || group.ColorSpace is PdfDeviceRgbColorSpaceConverter;

        if (blendsLikeRoot && !group.Knockout)
        {
            return pageGroup.Source.Recording;
        }
        else
        {
            return pageGroup;
        }
    }

    /// <summary>
    /// Writes <paramref name="region"/> of these premultiplied 8-bit RGBA samples into <paramref name="target"/>, the
    /// size of the region, composited over white paper: each color becomes c + 255 − α. The target holds 8-bit red,
    /// green and blue, followed by an alpha made opaque when it has one.
    /// </summary>
    public static void CopyOverPaper(this PdfImageSamples samples, in PdfIntegerRectangle region, PdfImageSamples target)
    {
        bool targetHasAlpha = target.Channels.Count == RasterBytesPerPixel;

        for (int row = 0; row < region.Height; row++)
        {
            Span<byte> sourceRow = samples.GetRow(region.Top + row).Slice(region.Left * RasterBytesPerPixel, region.Width * RasterBytesPerPixel);
            ReadOnlySpan<uint> sourcePixels = MemoryMarshal.Cast<byte, uint>(sourceRow);

            if (targetHasAlpha)
            {
                CopyRowOverPaper(sourcePixels, MemoryMarshal.Cast<byte, uint>(target.GetRow(row)));
            }
            else
            {
                CopyRowOverPaperWithoutAlpha(sourcePixels, target.GetRow(row));
            }
        }
    }

    /// <summary>
    /// Composites premultiplied RGBA pixels over white into opaque RGBA pixels. A premultiplied color never exceeds its
    /// alpha, so the sum never carries into the next component.
    /// </summary>
    private static void CopyRowOverPaper(in ReadOnlySpan<uint> source, in Span<uint> target)
    {
        ref uint firstSource = ref MemoryMarshal.GetReference(source);
        ref uint firstTarget = ref MemoryMarshal.GetReference(target);
        int count = source.Length;
        int index = 0;

#if !NETSTANDARD2_0
        Vector<uint> opaqueValues = new(OpaqueValue);
        Vector<uint> componentSpreads = new(ComponentSpread);
        Vector<uint> opaqueAlphas = new(OpaqueAlpha);

        for (; index <= count - Vector<uint>.Count; index += Vector<uint>.Count)
        {
            Vector<uint> values = Vector.LoadUnsafe(ref firstSource, (nuint)index);
            Vector<uint> backgrounds = (opaqueValues - Vector.ShiftRightLogical(values, AlphaShift)) * componentSpreads;

            ((values + backgrounds) | opaqueAlphas).StoreUnsafe(ref firstTarget, (nuint)index);
        }
#endif

        for (; index < count; index++)
        {
            Unsafe.Add(ref firstTarget, index) = ComposeOverPaper(Unsafe.Add(ref firstSource, index));
        }
    }

    /// <summary>
    /// Composites premultiplied RGBA pixels over white into RGB pixels. Every pixel but the last is written as four
    /// bytes, its fourth byte overwritten by the next pixel.
    /// </summary>
    private static void CopyRowOverPaperWithoutAlpha(in ReadOnlySpan<uint> source, in Span<byte> target)
    {
        ref uint firstSource = ref MemoryMarshal.GetReference(source);
        ref byte firstTarget = ref MemoryMarshal.GetReference(target);
        int lastIndex = source.Length - 1;

        for (int index = 0; index < lastIndex; index++)
        {
            Unsafe.WriteUnaligned(ref Unsafe.Add(ref firstTarget, index * RgbBytesPerPixel), ComposeOverPaper(Unsafe.Add(ref firstSource, index)));
        }

        if (lastIndex >= 0)
        {
            uint lastPixel = ComposeOverPaper(Unsafe.Add(ref firstSource, lastIndex));
            ref byte lastTarget = ref Unsafe.Add(ref firstTarget, lastIndex * RgbBytesPerPixel);

            lastTarget = (byte)lastPixel;
            Unsafe.Add(ref lastTarget, 1) = (byte)(lastPixel >> 8);
            Unsafe.Add(ref lastTarget, 2) = (byte)(lastPixel >> 16);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ComposeOverPaper(uint pixel)
        => (pixel + ((OpaqueValue - (pixel >> AlphaShift)) * ComponentSpread)) | OpaqueAlpha;
}
