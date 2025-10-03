namespace PdfReader.Rendering.Image.Jpg.Model
{
    /// <summary>
    /// Minimal component descriptor as declared in the JPEG SOF segment.
    /// </summary>
    internal sealed class JpgComponent
    {
        public byte Id { get; set; }
        public byte HorizontalSamplingFactor { get; set; }
        public byte VerticalSamplingFactor { get; set; }
        public byte QuantizationTableId { get; set; }
    }
}
