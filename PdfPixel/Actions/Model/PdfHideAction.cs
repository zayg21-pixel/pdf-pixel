using PdfPixel.Annotations;
using PdfPixel.Annotations.Model;
using PdfPixel.Models;
using PdfPixel.Text;
using System.Collections.Generic;

namespace PdfPixel.Actions.Model;

/// <summary>
/// Represents a Hide action that hides or shows annotations.
/// </summary>
public class PdfHideAction : PdfAction
{
    private readonly IPdfDocumentInternal _document;
    private List<PdfAnnotationBase>? _annotations;

    /// <summary>
    /// Initializes a new instance of the <see cref="PdfHideAction"/> class.
    /// </summary>
    /// <param name="actionDictionary">The PDF dictionary representing the Hide action.</param>
    internal PdfHideAction(PdfDictionary actionDictionary)
        : base(PdfActionType.Hide)
    {
        _document = actionDictionary.Document;
        AnnotationReferences = ReadAnnotationReferences(actionDictionary);
        Hide = actionDictionary.GetBoolean(PdfTokens.HideKey) ?? true;
    }

    // TODO: [MEDIUM] add support for field-name targets (/T text strings) with widgets implementation

    /// <summary>
    /// References of the annotations to hide or show (/T), in order; empty when the action names none.
    /// </summary>
    public IReadOnlyList<PdfReference> AnnotationReferences { get; }

    /// <summary>
    /// Whether the annotations are hidden (/H); <see langword="false"/> shows them.
    /// </summary>
    public bool Hide { get; }

    /// <summary>
    /// Returns the annotations at <see cref="AnnotationReferences"/>, in order.
    /// </summary>
    public IReadOnlyList<PdfAnnotationBase> GetAnnotations()
    {
        if (_annotations == null)
        {
            List<PdfAnnotationBase> annotations = new(AnnotationReferences.Count);

            foreach (PdfReference reference in AnnotationReferences)
            {
                PdfAnnotationBase? annotation = PdfAnnotationFactory.GetAnnotation(_document, reference);
                if (annotation != null)
                {
                    annotations.Add(annotation);
                }
            }

            _annotations = annotations;
        }

        return _annotations;
    }

    /// <summary>
    /// Returns a string representation of this Hide action.
    /// </summary>
    /// <returns>A string describing the action.</returns>
    public override string ToString() => "Hide Action";

    private static List<PdfReference> ReadAnnotationReferences(PdfDictionary actionDictionary)
    {
        List<PdfReference> references = [];
        PdfArray? targets = actionDictionary.GetArray(PdfTokens.HideTargetKey);

        if (targets == null)
        {
            PdfReference? reference = actionDictionary.GetReference(PdfTokens.HideTargetKey);
            if (reference != null)
            {
                references.Add(reference.Value);
            }

            return references;
        }

        for (int index = 0; index < targets.Count; index++)
        {
            PdfReference? reference = targets.GetReference(index);
            if (reference != null)
            {
                references.Add(reference.Value);
            }
        }

        return references;
    }
}
