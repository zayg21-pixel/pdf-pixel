namespace PdfPixel.Png.Parsing;

/// <summary>
/// Chunk types of the PNG specification, Third Edition, as big-endian 32-bit values of their four letters.
/// </summary>
internal static class PngChunkType
{
    public const uint ImageHeader = 0x49484452; // IHDR
    public const uint Palette = 0x504C5445; // PLTE
    public const uint ImageData = 0x49444154; // IDAT
    public const uint ImageEnd = 0x49454E44; // IEND
    public const uint Transparency = 0x74524E53; // tRNS
    public const uint IccProfile = 0x69434350; // iCCP
    public const uint SignificantBits = 0x73424954; // sBIT
    public const uint Background = 0x624B4744; // bKGD
    public const uint PhysicalDimensions = 0x70485973; // pHYs
    public const uint Exif = 0x65584966; // eXIf

    /// <summary>
    /// Bit 5 of the first type letter: set (lowercase) for ancillary chunks, clear (uppercase) for critical ones.
    /// </summary>
    public const uint AncillaryBit = 0x20000000;
}
