namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents an evaluated SFNT "glyf" table: a lazily-populated, GID-indexed cache of evaluated
/// glyphs. A glyph is resolved on demand (see <see cref="SfntGlyfProcessor.ResolveGlyph"/>) and cached
/// here afterward. A null entry means either "not yet resolved" or "resolved to no outline" (e.g.
/// space) - the two are not distinguished, so a glyph that failed to resolve is retried on every
/// access, which is rare enough not to matter.
/// </summary>
public class SfntGlyf
{
    private SfntGlyphCharacter?[]? _glyphs;

    /// <summary>
    /// Gets or sets this font's parsed "loca" table, used to look up each glyph's byte range within "glyf".
    /// </summary>
    internal SfntLoca Loca { get; set; } = new();

    /// <summary>
    /// Gets the number of glyphs, derived from <see cref="Loca"/>.
    /// </summary>
    public int NumGlyphs => Loca.Ranges.Count;

    /// <summary>
    /// Determines whether the given glyph id has already been resolved (successfully or not).
    /// </summary>
    internal bool Contains(int gid) => _glyphs != null && (uint)gid < (uint)_glyphs.Length && _glyphs[gid] != null;

    /// <summary>
    /// Gets the cached glyph for the given glyph id. Only meaningful after <see cref="Contains"/>
    /// returns <see langword="true"/> for that id.
    /// </summary>
    internal SfntGlyphCharacter? Get(int gid) => _glyphs?[gid];

    /// <summary>
    /// Caches the resolved glyph for the given glyph id.
    /// </summary>
    internal void Set(int gid, SfntGlyphCharacter? glyph)
    {
        _glyphs ??= new SfntGlyphCharacter?[NumGlyphs];
        _glyphs[gid] = glyph;
    }
}
