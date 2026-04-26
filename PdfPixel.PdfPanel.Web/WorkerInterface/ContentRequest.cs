using System;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class ContentRequest : RequestResponseHeader
{
    public Guid CancellationId { get; set; }
}
