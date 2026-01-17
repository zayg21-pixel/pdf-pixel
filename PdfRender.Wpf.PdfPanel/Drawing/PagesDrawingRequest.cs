using System;
using System.Linq;

namespace PdfRender.Wpf.PdfPanel.Drawing
{
    /// <summary>
    /// Request to draw pages on <see cref="SkiaPdfPanel"/>. Also triggers
    /// <see cref="PdfViewerPageCollection.OnAfterDraw"/>.
    /// </summary>
    internal class PagesDrawingRequest : DrawingRequest
    {
        public PdfViewerPageCollection Pages { get; set; }

        public VisiblePageInfo[] VisiblePages { get; set; }

        public System.Windows.Media.Color BackgroundColor { get; set; }

        public int MaxThumbnailSize { get; set; }

        public override bool Equals(object obj)
        {
            if (obj is PagesDrawingRequest parameters)
            {
                return base.Equals(obj) &&
                    MaxThumbnailSize == parameters.MaxThumbnailSize &&
                    Pages == parameters.Pages &&
                    VisiblePages.SequenceEqual(parameters.VisiblePages);
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