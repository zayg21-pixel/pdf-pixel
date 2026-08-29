using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Annotations.Models;

/// <summary>
/// Parsed annotation appearance dictionary (AP), holding the appearance stream of each visual state.
/// </summary>
/// <remarks>
/// A visual state whose entry holds a sub-dictionary of appearance streams instead of a single stream
/// is resolved through the annotation's appearance state (AS).
/// </remarks>
public sealed class PdfAnnotationAppearance
{
    private PdfAnnotationAppearance(PdfObject? normal, PdfObject? rollover, PdfObject? down)
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
    public PdfObject? Normal { get; }

    /// <summary>
    /// Gets the appearance stream of the rollover state (R).
    /// </summary>
    public PdfObject? Rollover { get; }

    /// <summary>
    /// Gets the appearance stream of the down state (D).
    /// </summary>
    public PdfObject? Down { get; }

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
    public static PdfAnnotationAppearance? FromDictionary(PdfDictionary? appearanceDictionary, PdfString? appearanceState)
    {
        if (appearanceDictionary == null)
        {
            return null;
        }

        PdfObject? normal = ResolveState(appearanceDictionary, PdfTokens.NKey, appearanceState);
        PdfObject? rollover = ResolveState(appearanceDictionary, PdfTokens.RolloverKey, appearanceState);
        PdfObject? down = ResolveState(appearanceDictionary, PdfTokens.DownKey, appearanceState);

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
    /// <returns>The appearance stream object, or null.</returns>
    public PdfObject? GetStream(PdfAnnotationVisualStateKind visualStateKind)
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
    private static PdfObject? ResolveState(PdfDictionary appearanceDictionary, in PdfString stateKey, PdfString? appearanceState)
    {
        PdfObject? stateObject = appearanceDictionary.GetObject(stateKey);

        if (stateObject == null)
        {
            return null;
        }

        if (stateObject.HasStream)
        {
            return stateObject;
        }

        if (appearanceState == null)
        {
            return null;
        }

        PdfObject? selectedObject = stateObject.Dictionary.GetObject(appearanceState.Value);

        if (selectedObject == null || !selectedObject.HasStream)
        {
            return null;
        }

        return selectedObject;
    }
}
