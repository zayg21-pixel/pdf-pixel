using System;

namespace PdfPixel.Fonts.Sfnt;

/// <summary>
/// Represents an evaluated SFNT "glyf" table: a lazily-populated, GID-indexed cache of glyph paths.
/// A path is emitted on demand (see <see cref="SfntGlyfProcessor.ResolvePath"/>) and cached here
/// afterward. An empty entry means either "not yet emitted" or "emitted to no outline" (e.g. space) -
/// the two are not distinguished, so such a glyph is evaluated again on every access, which costs
/// nothing for the glyph without outline bytes that it almost always is.
/// Repacking does not read this cache: it walks every glyph exactly once and keeps none of them.
/// </summary>
public class SfntGlyf
{
    private ReadOnlyMemory<byte>[]? _paths;

    /// <summary>
    /// Gets this font's parsed "loca" table, used to look up each glyph's byte range within "glyf"; empty until
    /// <see cref="SetLoca"/>.
    /// </summary>
    internal SfntLoca Loca { get; private set; } = new();

    /// <summary>
    /// Gets whether <see cref="Loca"/> has been parsed.
    /// </summary>
    internal bool IsLocaResolved { get; private set; }

    /// <summary>
    /// Gets the number of glyphs, derived from <see cref="Loca"/>.
    /// </summary>
    internal int NumGlyphs => Loca.Ranges.Count;

    /// <summary>
    /// Sets the parsed "loca" table.
    /// </summary>
    internal void SetLoca(SfntLoca loca)
    {
        Loca = loca;
        IsLocaResolved = true;
    }

    /// <summary>
    /// Gets the cached path for the given glyph id, empty when none has been emitted for it yet.
    /// </summary>
    internal ReadOnlyMemory<byte> GetPath(int gid)
    {
        if (_paths == null || (uint)gid >= (uint)_paths.Length)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        return _paths[gid];
    }

    /// <summary>
    /// Caches the emitted path for the given glyph id.
    /// </summary>
    internal void SetPath(int gid, in ReadOnlyMemory<byte> path)
    {
        if (_paths == null)
        {
            _paths = new ReadOnlyMemory<byte>[NumGlyphs];
        }

        if ((uint)gid < (uint)_paths.Length)
        {
            _paths[gid] = path;
        }
    }
}
