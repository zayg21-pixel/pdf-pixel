using PdfPixel.Models;

namespace PdfPixel.PdfPanel.ContentProvider;

public class UpdateContentRequest
{
    public int[] VisiblePages { get; set; }

    public PdfRenderingParameters RenderingParameters { get; set; }

    public PdfPanelPointerState PointerState { get; set; }

    public PdfAnnotationPopup ActiveAnnotation { get; set; }
}
