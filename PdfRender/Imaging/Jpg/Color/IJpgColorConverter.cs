using PdfRender.Imaging.Jpg.Model;

namespace PdfRender.Imaging.Jpg.Color;

/// <summary>
/// Abstraction for in-place component color conversion on a single MCU-row band.
/// Implementations mutate the supplied upsampled full-resolution component blocks (e.g. YCbCr -> RGB).
/// Packing to an interleaved byte buffer is handled by the caller/decoder.
/// </summary>
internal interface IJpgColorConverter
{
    /// <summary>
    /// Convert the supplied upsampled component blocks in-place to the target color space representation.
    /// The array layout: upsampledBandBlocks[component][blockIndex] where each block is 8x8 of floats in 0..255 range.
    /// Implementations may overwrite component ordering (e.g. store R,G,B back into slots 0,1,2).
    /// </summary>
    /// <param name="upsampledBandBlocks">Per-component arrays of full-resolution 8x8 blocks.</param>
    void ConvertInPlace(Block8x8F[][] upsampledBandBlocks);
}
