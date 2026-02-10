namespace PdfPixel.Text
{
    /// <summary>
    /// Text rendering modes for PDF text operators
    /// Based on PDF Reference Section 5.3.11
    /// </summary>
    public enum PdfTextRenderingMode
    {
        /// <summary>
        /// Fill text (mode 0)
        /// </summary>
        Fill = 0,

        /// <summary>
        /// Stroke text (mode 1)
        /// </summary>
        Stroke = 1,

        /// <summary>
        /// Fill and stroke text (mode 2)
        /// </summary>
        FillAndStroke = 2,

        /// <summary>
        /// Invisible text (mode 3)
        /// </summary>
        Invisible = 3,

        /// <summary>
        /// Fill text and add to path for clipping (mode 4)
        /// </summary>
        FillAndClip = 4,

        /// <summary>
        /// Stroke text and add to path for clipping (mode 5)
        /// </summary>
        StrokeAndClip = 5,

        /// <summary>
        /// Fill and stroke text and add to path for clipping (mode 6)
        /// </summary>
        FillAndStrokeAndClip = 6,

        /// <summary>
        /// Add text to path for clipping (mode 7)
        /// </summary>
        Clip = 7
    }
}