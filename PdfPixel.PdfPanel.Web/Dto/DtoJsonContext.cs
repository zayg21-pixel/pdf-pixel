using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Source-generated JSON serialization of the DTOs exchanged with JS, with camelCase property names.
/// </summary>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(DocumentResultDto))]
[JsonSerializable(typeof(DocumentErrorDto))]
[JsonSerializable(typeof(CredentialRequestedResultDto))]
[JsonSerializable(typeof(RedrawInputDto))]
[JsonSerializable(typeof(RedrawResultDto))]
[JsonSerializable(typeof(FrameDto))]
[JsonSerializable(typeof(PageDestinationDto))]
[JsonSerializable(typeof(NamedDestinationDto))]
[JsonSerializable(typeof(List<ThumbnailLayoutDto>))]
internal sealed partial class DtoJsonContext : JsonSerializerContext
{
}
