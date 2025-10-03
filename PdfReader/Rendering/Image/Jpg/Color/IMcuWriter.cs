using System;

namespace PdfReader.Rendering.Image.Jpg.Color
{
    /// <summary>
    /// Interface for writing MCU (Minimum Coded Unit) data to an RGBA buffer.
    /// Implementations handle color space conversion from decoded JPEG component tiles.
    /// </summary>
    internal interface IMcuWriter
    {
        /// <summary>
        /// Write MCU pixels to the output buffer in RGBA format.
        /// </summary>
        /// <param name="buffer">Output RGBA buffer.</param>
        /// <param name="xBase">Starting X coordinate in the output image.</param>
        /// <param name="heightPixels">Number of pixel rows to write.</param>
        void WriteToBuffer(byte[] buffer, int xBase, int heightPixels);
    }
}