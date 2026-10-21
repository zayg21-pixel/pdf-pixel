using PdfPixel.Forms;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using PdfPixel.Resources;

namespace PdfPixel.Annotations;

/// <summary>
/// Parsed annotation appearance dictionary (AP), holding the appearance stream of each visual state.
/// </summary>
/// <remarks>
/// A visual state whose entry holds a sub-dictionary of appearance streams instead of a single stream
/// is resolved through the annotation's appearance state (AS).
/// </remarks>
public sealed class PdfAnnotationAppearance
{
    private PdfAnnotationAppearance(PdfForm? normal, PdfForm? rollover, PdfForm? down)
    {
        Normal = normal;
        Rollover = rollover;
        Down = down;

        var supportedStates = PdfAnnotationVisualStateKind.None;

        if (normal != null)
        {
            supportedStates |= PdfAnnotationVisualStateKind.Normal;
        }

        if (rollover != null)
        {
            supportedStates |= PdfAnnotationVisualStateKind.Rollover;
        }

        if (down != null)
        {
            supportedStates |= PdfAnnotationVisualStateKind.Down;
        }

        SupportedStates = supportedStates;
    }

    /// <summary>
    /// Gets the appearance stream of the normal state (N).
    /// </summary>
    public PdfForm? Normal { get; }

    /// <summary>
    /// Gets the appearance stream of the rollover state (R).
    /// </summary>
    public PdfForm? Rollover { get; }

    /// <summary>
    /// Gets the appearance stream of the down state (D).
    /// </summary>
    public PdfForm? Down { get; }

    /// <summary>
    /// Gets the visual states this appearance carries a stream for.
    /// </summary>
    public PdfAnnotationVisualStateKind SupportedStates { get; }

    /// <summary>
    /// Creates the parsed appearance of an annotation.
    /// </summary>
    /// <param name="appearanceDictionary">The appearance dictionary (AP), or null when the annotation carries none.</param>
    /// <param name="appearanceState">The appearance state (AS) that selects the stream inside a state sub-dictionary.</param>
    /// <returns>The parsed appearance, or null when no visual state resolves to an appearance stream.</returns>
    internal static PdfAnnotationAppearance? FromDictionary(PdfDictionary? appearanceDictionary, PdfString? appearanceState)
    {
        if (appearanceDictionary == null)
        {
            return null;
        }

        PdfForm? normal = ResolveState(appearanceDictionary, PdfTokens.NKey, appearanceState);
        PdfForm? rollover = ResolveState(appearanceDictionary, PdfTokens.RolloverKey, appearanceState);
        PdfForm? down = ResolveState(appearanceDictionary, PdfTokens.DownKey, appearanceState);

        if (normal == null && rollover == null && down == null)
        {
            return null;
        }

        return new PdfAnnotationAppearance(normal, rollover, down);
    }

    /// <summary>
    /// Returns the appearance stream to render for the requested visual state, after falling back to
    /// the states this appearance does carry, or null when it carries none of them.
    /// </summary>
    /// <param name="visualStateKind">The requested visual state.</param>
    /// <returns>The appearance stream form, or null.</returns>
    public PdfForm? GetStream(PdfAnnotationVisualStateKind visualStateKind)
    {
        return visualStateKind switch
        {
            PdfAnnotationVisualStateKind.Down => Down ?? Rollover ?? Normal,
            PdfAnnotationVisualStateKind.Rollover => Rollover ?? Normal,
            _ => Normal
        };
    }

    /// <summary>
    /// Resolves a single appearance state entry to its stream, selecting through
    /// <paramref name="appearanceState"/> when the entry holds a sub-dictionary of streams.
    /// </summary>
    private static PdfForm? ResolveState(PdfDictionary appearanceDictionary, in PdfString stateKey, PdfString? appearanceState)
    {
        PdfXObject? stateXObject = PdfXObject.GetXObject(appearanceDictionary, stateKey);

        if (stateXObject != null)
        {
            return stateXObject as PdfForm;
        }

        if (appearanceState == null)
        {
            return null;
        }

        PdfDictionary? stateDictionary = appearanceDictionary.GetDictionary(stateKey);

        if (stateDictionary == null)
        {
            return null;
        }

        return PdfXObject.GetXObject(stateDictionary, appearanceState.Value) as PdfForm;
    }
}
