using PdfPixel.Commands.Cache;
using PdfPixel.Fonts.Model;
using System;
using System.Runtime.CompilerServices;

namespace PdfPixel.Skia.Cache;

/// <summary>
/// Identifies a <see cref="SkTypefaceCommandCacheEntry"/> for a given <see cref="IPdfTypeface"/>.
/// </summary>
internal sealed class TypefaceCommandCacheKey : ICommandCacheKey
{
    private readonly IPdfTypeface _typeface;

    public TypefaceCommandCacheKey(IPdfTypeface typeface) => _typeface = typeface ?? throw new ArgumentNullException(nameof(typeface));

    public bool Equals(ICommandCacheKey? other) => other is TypefaceCommandCacheKey key && ReferenceEquals(_typeface, key._typeface);

    public override bool Equals(object? obj) => Equals(obj as ICommandCacheKey);

    public override int GetHashCode() => RuntimeHelpers.GetHashCode(_typeface);
}
