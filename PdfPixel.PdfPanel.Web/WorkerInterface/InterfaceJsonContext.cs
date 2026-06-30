using PdfPixel.PdfPanel.Web.WorkerCommands;
using System.Text.Json.Serialization;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Worker interaction JSON context.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WebDocumentData))]
[JsonSerializable(typeof(SetFontRequest))]
[JsonSerializable(typeof(UpdateContentRequest))]
[JsonSerializable(typeof(WebDrawingRequest))]
[JsonSerializable(typeof(WebVisiblePageInfo))]
[JsonSerializable(typeof(WebDocumentPageInfo))]
[JsonSerializable(typeof(RefreshCacheRequest))]
[JsonSerializable(typeof(UpdateContentResponseHeader))]
[JsonSerializable(typeof(WebRect))]
[JsonSerializable(typeof(WebAnnotationPopupData))]
[JsonSerializable(typeof(WebAnnotationNavigationData))]
[JsonSerializable(typeof(WebAnnotationDestinationData))]
[JsonSerializable(typeof(WebAnnotationMessageData))]
internal partial class InterfaceJsonContext : JsonSerializerContext
{
}
