using PdfPixel.Commands.Cache;
using PdfPixel.Shading;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Identifies a <see cref="ShadingCommandCacheEntry"/> built for one shading content, with its colors converted
/// by one color sampler.
/// </summary>
internal sealed class ShadingCommandCacheKey : ICommandCacheKey
{
    private readonly PdfShadingContent _content;
    private readonly SkiaColorSampler _colorSampler;

    public ShadingCommandCacheKey(PdfShadingContent content, SkiaColorSampler colorSampler)
    {
        _content = content ?? throw new ArgumentNullException(nameof(content));
        _colorSampler = colorSampler ?? throw new ArgumentNullException(nameof(colorSampler));
    }

    public bool Equals(ICommandCacheKey? other)
    {
        return other is ShadingCommandCacheKey key
            && ReferenceEquals(_content, key._content)
            && ReferenceEquals(_colorSampler, key._colorSampler);
    }

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => HashCode.Combine(RuntimeHelpers.GetHashCode(_content), RuntimeHelpers.GetHashCode(_colorSampler));
}
