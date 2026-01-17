using System.Windows;
using System.Windows.Media.Imaging;

namespace PdfRender.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Request to draw on <see cref="SkiaPdfPanel"/>.
    /// </summary>
    internal abstract class DrawingRequest
    {
        public double Scale { get; set; }

        public Point Offset { get; set; }

        public Size CanvasSize { get; set; }

        public Point CanvasScale { get; set; }

        public Point CanvasOffset { get; set; }

        public WriteableBitmap WritableBitmap { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is DrawingRequest request)
            {
                return Scale == request.Scale &&
                    Offset == request.Offset &&
                    CanvasSize == request.CanvasSize &&
                    CanvasScale == request.CanvasScale &&
                    CanvasOffset == request.CanvasOffset &&
                    WritableBitmap == request.WritableBitmap;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }
}