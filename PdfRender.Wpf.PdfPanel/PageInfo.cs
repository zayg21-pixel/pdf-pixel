using System.Windows;

namespace PdfRender.Wpf.PdfPanel
{
    /// <summary>
    /// Information about a page in a PDF document.
    /// </summary>
    public readonly struct PageInfo
    {
        public PageInfo(double width, double height, int rotation)
        {
            Width = width;
            Height = height;
            Rotation = rotation;
        }

        /// <summary>
        /// Original page width without rotation.
        /// </summary>
        public double Width { get; }

        /// <summary>
        /// Original page height without rotation.
        /// </summary>
        public double Height { get; }

        /// <summary>
        /// Page rotation in degrees.
        /// </summary>
        public int Rotation { get; }

        /// <summary>
        /// Get the total rotation of the page after user rotation.
        /// </summary>
        /// <param name="userRotation">User rotation in degrees.</param>
        /// <returns></returns>
        public int GetTotalRotation(int userRotation)
        {
            var totalRotation = Rotation + userRotation;
            totalRotation = totalRotation % 360;

            if (totalRotation < 0)
            {
                totalRotation += 360;
            }

            return totalRotation;
        }

        /// <summary>
        /// Returns the size of the page after user rotation.
        /// </summary>
        /// <param name="userRotation">User rotation in degrees.</param>
        /// <returns></returns>
        public Size GetRotatedSize(int userRotation)
        {
            var totalRotation = GetTotalRotation(userRotation);

            if (totalRotation % 180 != 0)
            {
                return new Size(Height, Width);
            }

            return new Size(Width, Height);
        }
    }
}
