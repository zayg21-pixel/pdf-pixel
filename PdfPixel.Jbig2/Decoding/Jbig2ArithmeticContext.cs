using System;
using PdfPixel.Jbig2.Model;

namespace PdfPixel.Jbig2.Decoding;

/// <summary>
/// Holds the QM-coder probability context arrays for one text-region arithmetic decode session
/// (ITU-T T.88 Annex A). Each array is a 512-entry (or variable-size) probability state vector
/// for a named integer-coding procedure. Context arrays must persist across symbol instances so
/// the probability model stays synchronized with the encoder.
/// </summary>
/// <remarks>
/// Arrays whose size depends on decode parameters (<see cref="IaId"/>, <see cref="Gr"/>).
/// The <see cref="IaId"/>, <see cref="Iardx"/>, <see cref="Iardy"/>, and
/// <see cref="Gr"/> arrays may be injected from an owning decoder (inline aggregate path,
/// ITU-T T.88 Section 6.5.8.2) to keep the probability model continuous across symbol decodes.
/// </remarks>
internal sealed class Jbig2ArithmeticContext
{
    /// <summary>
    /// IADT – strip delta-T integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iadt;

    /// <summary>
    /// IAFS – first-S integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iafs;

    /// <summary>
    /// IADS – delta-S integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iads;

    /// <summary>
    /// IAIT – instance-T integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iait;

    /// <summary>
    /// IARI – refinement indicator integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iari;

    /// <summary>
    /// IARDW – refinement delta-width integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iardw;

    /// <summary>
    /// IARDH – refinement delta-height integer coder context (512 states).
    /// </summary>
    public readonly byte[] Iardh;

    /// <summary>
    /// IAID – symbol-ID integer coder context. Size = <c>1 &lt;&lt; (symbolCodeLength + 1)</c>.
    /// May be injected from a parent decoder to persist probability state across symbols.
    /// </summary>
    public readonly byte[] IaId;

    /// <summary>
    /// IARDX – refinement delta-X integer coder context (512 states).
    /// May be injected from a parent decoder.
    /// </summary>
    public readonly byte[] Iardx;

    /// <summary>
    /// IARDY – refinement delta-Y integer coder context (512 states).
    /// May be injected from a parent decoder.
    /// </summary>
    public readonly byte[] Iardy;

    /// <summary>
    /// GR – generic refinement region context. Size = <c>1 &lt;&lt; 13</c> for template 0,
    /// <c>1 &lt;&lt; 10</c> for template 1. May be injected from a parent decoder.
    /// </summary>
    public readonly byte[] Gr;

    /// <summary>
    /// Refinement template identifier (0 or 1). Determines <see cref="Gr"/> array size
    /// and which AT pixel offsets are active.
    /// </summary>
    public readonly int RefinementTemplate;

    /// <summary>
    /// Refinement row template built from <see cref="RefinementTemplate"/> and the refinement
    /// adaptive template pixel offsets.
    /// </summary>
    public readonly Jbig2RowTemplate RefinementRowTemplate;

    /// <summary>
    /// Symbol-ID code length (SYMCODELEN). Determines <see cref="IaId"/> array size.
    /// </summary>
    public readonly int SymbolCodeLength;

    // ── Safety limits ────────────────────────────────────────────────────────

    /// <summary>
    /// Maximum inline decode depth allowed. Prevents infinite loops caused by
    /// corrupt data or mis-synchronised contexts across text-region, refinement
    /// and symbol-dictionary decode chains.
    /// </summary>
    public const int InlineLimit = 256;

    /// <summary>
    /// Current inline decode depth. Incremented before each inline text-region
    /// or refinement call, decremented after. When this exceeds
    /// <see cref="InlineLimit"/> the caller should bail out.
    /// </summary>
    public int InlineLevel;

    // ── Placement parameters ─────────────────────────────────────────────────

    /// <summary>
    /// Text-region placement flags that drive symbol positioning.
    /// Assigned at construction for normal text-region decodes, or overwritten with
    /// <see cref="Jbig2TextRegionFlags.DefaultInlineFlags"/> for inline aggregate decodes
    /// (ITU-T T.88 Section 6.5.8.2).
    /// </summary>
    public Jbig2TextRegionFlags PlacementFlags;

    /// <summary>
    /// Initialises a context, allocating all arrays fresh.
    /// </summary>
    /// <param name="symbolCodeLength">IAID code length; determines <see cref="IaId"/> array size.</param>
    /// <param name="refinementTemplate">Refinement template (0 or 1); determines <see cref="Gr"/> array size.</param>
    /// <param name="refinementAtX">Refinement adaptive template pixel X offsets.</param>
    /// <param name="refinementAtY">Refinement adaptive template pixel Y offsets.</param>
    /// <param name="existingGr">
    /// Optional existing GR_stats array retained from a prior decode session.
    /// When non-null, this array is reused instead of allocating a fresh one
    /// (ITU-T T.88 Section 7.4.2.2 step 3).
    /// </param>
    public Jbig2ArithmeticContext(
    int symbolCodeLength,
    int refinementTemplate,
    sbyte[]? refinementAtX = null,
    sbyte[]? refinementAtY = null,
    byte[]? existingGr = null)
    {
        Iadt = new byte[512];
        Iafs = new byte[512];
        Iads = new byte[512];
        Iait = new byte[512];
        Iari = new byte[512];
        Iardw = new byte[512];
        Iardh = new byte[512];
        IaId = new byte[1 << (symbolCodeLength + 1)];
        Iardx = new byte[512];
        Iardy = new byte[512];
        Gr = existingGr ?? new byte[(refinementTemplate == 0) ? 1 << 13 : 1 << 10];
        RefinementTemplate = refinementTemplate;
        RefinementRowTemplate = Jbig2Templates.BuildRefinementTemplate(refinementTemplate, refinementAtX ?? new sbyte[2], refinementAtY ?? new sbyte[2]);
        SymbolCodeLength = symbolCodeLength;
    }

    /// <summary>
    /// Resets only the seven placement-loop context arrays (IADT, IAFS, IADS, IAIT, IARI,
    /// IARDW, IARDH) to zero. The four shared arrays (<see cref="IaId"/>, <see cref="Iardx"/>,
    /// <see cref="Iardy"/>, <see cref="Gr"/>) are left intact so their probability state
    /// remains continuous across inline aggregate symbol decodes.
    /// </summary>
    public void ResetPlacementContexts()
    {
        Array.Clear(Iadt, 0, Iadt.Length);
        Array.Clear(Iafs, 0, Iafs.Length);
        Array.Clear(Iads, 0, Iads.Length);
        Array.Clear(Iait, 0, Iait.Length);
        Array.Clear(Iari, 0, Iari.Length);
        Array.Clear(Iardw, 0, Iardw.Length);
        Array.Clear(Iardh, 0, Iardh.Length);
    }

    /// <summary>
    /// Resets all context arrays to zero so the decoder can be reused without re-allocation.
    /// Do not call this between dependent inline aggregate symbol decodes — use
    /// <see cref="ResetPlacementContexts"/> instead.
    /// </summary>
    public void Clear()
    {
        ResetPlacementContexts();
        Array.Clear(IaId, 0, IaId.Length);
        Array.Clear(Iardx, 0, Iardx.Length);
        Array.Clear(Iardy, 0, Iardy.Length);
        Array.Clear(Gr, 0, Gr.Length);
    }

    }
