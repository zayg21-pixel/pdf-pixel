using PdfPixel.Files;
using PdfPixel.Models;
using PdfPixel.Text;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a Thread action that jumps to a bead of an article thread, as written.
/// </summary>
public class PdfThreadAction : PdfAction
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PdfThreadAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the Thread action.</param>
    internal PdfThreadAction(PdfDictionary actionDictionary)
        : base(PdfActionType.Thread)
    {
        FileSpecification = PdfFileSpecification.GetFileSpecification(actionDictionary, PdfTokens.FKey);
        ThreadReference = actionDictionary.GetReference(PdfTokens.ThreadActionThreadKey);
        ThreadIndex = actionDictionary.GetInteger(PdfTokens.ThreadActionThreadKey);
        ThreadTitle = actionDictionary.GetString(PdfTokens.ThreadActionThreadKey);
        BeadReference = actionDictionary.GetReference(PdfTokens.ThreadActionBeadKey);
        BeadIndex = actionDictionary.GetInteger(PdfTokens.ThreadActionBeadKey);
    }

    /// <summary>
    /// File holding the thread (/F), or <see langword="null"/> when the thread is in this document.
    /// </summary>
    public PdfFileSpecification? FileSpecification { get; }

    /// <summary>
    /// Reference of the thread dictionary (/D written as a reference), or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? ThreadReference { get; }

    /// <summary>
    /// Zero-based index of the thread in the document's threads (/D written as an integer), or
    /// <see langword="null"/> when absent.
    /// </summary>
    public int? ThreadIndex { get; }

    /// <summary>
    /// Title of the thread (/D written as a string), or <see langword="null"/> when absent.
    /// </summary>
    public PdfString? ThreadTitle { get; }

    /// <summary>
    /// Reference of the bead dictionary (/B written as a reference), or <see langword="null"/> when absent.
    /// </summary>
    public PdfReference? BeadReference { get; }

    /// <summary>
    /// Zero-based index of the bead in its thread (/B written as an integer), or <see langword="null"/> when absent.
    /// </summary>
    public int? BeadIndex { get; }

    /// <summary>
    /// Returns a string representation of this Thread action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "Thread Action";
}
