using System.Text.Json.Serialization;

namespace PdfPixel.PdfPanel.Web.Dto;

/// <summary>
/// Action, written with its <c>type</c>.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(UriActionDto), "uri")]
[JsonDerivedType(typeof(LaunchActionDto), "launch")]
[JsonDerivedType(typeof(GoToRemoteActionDto), "goToRemote")]
[JsonDerivedType(typeof(NamedActionDto), "named")]
[JsonDerivedType(typeof(UnsupportedActionDto), "unsupported")]
internal abstract class ActionDto
{
}
