namespace PdfPixel.Png.Model;

/// <summary>
/// Byte range of the data of one IDAT chunk inside a PNG file.
/// </summary>
internal readonly struct PngDataSegment
{
    public PngDataSegment(int offset, int length)
    {
        Offset = offset;
        Length = length;
    }

    public int Offset { get; }

    public int Length { get; }
}
