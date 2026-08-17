using PdfPixel.Commands.Cache;
using PdfPixel.Shading.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Identifies a <see cref="ShadingCommandCacheEntry"/> built for one shading content.
/// </summary>
internal sealed class ShadingCommandCacheKey : ICommandCacheKey
{
    private readonly PdfShadingContent _content;

    public ShadingCommandCacheKey(PdfShadingContent content) => _content = content ?? throw new ArgumentNullException(nameof(content));

    public bool Equals(ICommandCacheKey? other)
        => other is ShadingCommandCacheKey key && ReferenceEquals(_content, key._content);

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(_content);
}
