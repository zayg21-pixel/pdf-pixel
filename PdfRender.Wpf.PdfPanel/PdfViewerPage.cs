
namespace PdfRender.Wpf.PdfPanel
{
    /// <summary>
    /// Represents a page in the PDF document in <see cref="PdfViewerPageCollection"/>.
    /// </summary>
    public class PdfViewerPage
    {
        internal PdfViewerPage(PageInfo info, int pageNumber)
        {
            Info = info;
            PageNumber = pageNumber;
        }

        /// <summary>
        /// Information about the page, it's size and rotation.
        /// </summary>
        public PageInfo Info { get; set; }

        /// <summary>
        /// Number of the page.
        /// </summary>
        public int PageNumber { get; }

        /// <summary>
        /// User defined rotation of the page in degrees.
        /// Can be any value that is a multiple of 90.
        /// </summary>
        public int UserRotation { get; set; }

        internal bool IsVisible(double offset, double canvasHeight)
        {
            var pageHeight = Info.GetRotatedSize(UserRotation).Height;
            var pageTop = offset;
            var pageBottom = offset + pageHeight;

            return (pageTop >= 0 && pageTop <= canvasHeight) || (pageBottom >= 0 && pageBottom <= canvasHeight) || (pageTop <= 0 && pageBottom >= canvasHeight);
        }

        internal bool IsCurrent(double offset, double pageGap, double canvasHeight)
        {
            var pageHeight = Info.GetRotatedSize(UserRotation).Height;
            var pageTop = offset;
            var pageBottom = offset + pageHeight + pageGap;

            return (pageTop >= -pageGap && pageTop <= canvasHeight / 2) || (pageTop <= -pageGap && pageBottom >= canvasHeight / 2);
        }
    }
}
