using PdfReader.Rendering.Color.Clut;

namespace PdfReader.Rendering.Image.Processing
{
    /// <summary>
    /// Defines a contract for decoding a row of image data into RGBA pixel format.
    /// </summary>
    internal interface IRgbaRowDecoder
    {
        /// <summary>
        /// Decodes a row of image data from the source buffer into the destination RGBA buffer.
        /// </summary>
        /// <param name="source">Reference to the first byte of the source row data.</param>
        /// <param name="destination">Reference to the first RGBA pixel of the destination buffer.</param>
        void Decode(ref byte source, ref Rgba destination);
    }
}
