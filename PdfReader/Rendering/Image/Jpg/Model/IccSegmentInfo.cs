namespace PdfReader.Rendering.Image.Jpg.Model
{
    /// <summary>
    /// ICC segment bookkeeping for APP2 ICC_PROFILE; the actual profile bytes can be reconstructed by caller.
    /// </summary>
    internal sealed class IccSegmentInfo
    {
        public int SequenceNumber { get; set; }
        public int TotalSegments { get; set; }
        public byte[] Data { get; set; }
    }
}
