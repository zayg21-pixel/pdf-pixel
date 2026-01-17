using System.Windows;
using System.Windows.Input;

namespace PdfRender.Wpf.PdfPanel
{
    /// <summary>
    /// Represents the event data for mouse events on the <see cref="SkiaPdfPanel"/>.
    /// </summary>
    public class CanvasMouseEventArgs : RoutedEventArgs
    {
        public CanvasMouseEventArgs(RoutedEvent routedEvent, SkiaPdfPanel source, Point positionOnCanvas, int? pageNumber, Point? positionOnPage, MouseEventArgs mouseEventArgs)
            : base(routedEvent, source)
        {
            PositionOnCanvas = positionOnCanvas;
            MouseEventArgs = mouseEventArgs;
            PageNumber = pageNumber;
            PositionOnPage = positionOnPage;
            Scale = source.Scale;
            Offset = new Point(source.HorizontalOffset, source.VerticalOffset);
        }

        /// <summary>
        /// Mouse position on the canvas.
        /// </summary>
        public Point PositionOnCanvas { get; }

        /// <summary>
        /// Original mouse event arguments.
        /// </summary>
        public MouseEventArgs MouseEventArgs { get; }

        /// <summary>
        /// Page number under the mouse cursor.
        /// Null if no page is under the cursor.
        /// </summary>
        public int? PageNumber { get; }

        /// <summary>
        /// Mouse position on the page in not rotated page coordinates.
        /// Null if no page is under the cursor.
        /// </summary>
        public Point? PositionOnPage { get; }

        /// <summary>
        /// Viewer scale.
        /// </summary>
        public double Scale { get; }

        /// <summary>
        /// Viewer offset.
        /// </summary>
        public Point Offset { get; }
    }
}
