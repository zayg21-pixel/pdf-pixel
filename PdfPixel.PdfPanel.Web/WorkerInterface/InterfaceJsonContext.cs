using PdfPixel.PdfPanel.Web.WorkerCommands;
using System.Text.Json.Serialization;

namespace PdfPixel.PdfPanel.Web.WorkerInterface;

/// <summary>
/// Worker interaction JSON context.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = false)]
[JsonSerializable(typeof(WebDocumentData))]
[JsonSerializable(typeof(SetFontRequest))]
[JsonSerializable(typeof(RequestHeader))]
[JsonSerializable(typeof(UpdateContentRequest))]
[JsonSerializable(typeof(SetDocumentRequest))]
[JsonSerializable(typeof(WebDocumentPageInfo))]
[JsonSerializable(typeof(UpdateCacheRequest))]
internal partial class InterfaceJsonContext : JsonSerializerContext
{
}
