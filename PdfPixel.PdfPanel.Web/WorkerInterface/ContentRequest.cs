using System;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

public class ContentRequest : RequestHeader
{
    public Guid CancellationId { get; set; }
}
