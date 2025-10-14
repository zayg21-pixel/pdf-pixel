namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Defines a contract for decoding a row of image data into grayscale (byte) format.
    /// </summary>
    internal interface IGrayRowDecoder
    {
        /// <summary>
        /// Decodes a row of image data from the source buffer into the destination grayscale buffer.
        /// </summary>
        /// <param name="source">Reference to the first byte of the source row data.</param>
        /// <param name="destination">Reference to the first byte of the destination grayscale buffer.</param>
        void Decode(ref byte source, ref byte destination);
    }
}
