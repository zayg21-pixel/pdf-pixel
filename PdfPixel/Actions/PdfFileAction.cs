using PdfPixel.Files;
using PdfPixel.Objects;
using PdfPixel.Parsing;

namespace PdfPixel.Actions;

/// <summary>
/// Action that refers to another file.
/// </summary>
public abstract class PdfFileAction : PdfAction
{
    private protected PdfFileAction(PdfActionType actionType, PdfDictionary actionDictionary)
        : base(actionType)
    {
        FileSpecification = PdfFileSpecification.GetFileSpecification(actionDictionary, PdfTokens.FKey);
        NewWindow = actionDictionary.GetBoolean(PdfTokens.NewWindowKey);
    }

    private protected PdfFileAction(PdfActionType actionType, PdfFileSpecification fileSpecification)
        : base(actionType)
    {
        FileSpecification = fileSpecification;
    }

    /// <summary>
    /// File the action refers to (/F), or <see langword="null"/> when absent.
    /// </summary>
    public PdfFileSpecification? FileSpecification { get; }

    /// <summary>
    /// Whether to open the document in a new window (/NewWindow), or <see langword="null"/> when the
    /// viewer decides.
    /// </summary>
    public bool? NewWindow { get; }
}
