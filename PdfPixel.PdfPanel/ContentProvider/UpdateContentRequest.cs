using PdfPixel.Models;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.ContentProvider;

public class UpdateContentRequest
{
    public List<int> VisiblePages { get; set; }

    public PdfRenderingParameters RenderingParameters { get; set; }

    public PdfPanelPointerState PointerState { get; set; }

    public PdfAnnotationPopup ActiveAnnotation { get; set; }

}
