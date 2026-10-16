using PdfPixel.Actions.Model;
using PdfPixel.Annotations.Model;
using PdfPixel.Models;
using PdfPixel.Navigation.Model;
using PdfPixel.Parsing;
using PdfPixel.Text;
using System;
using System.Collections.Generic;

namespace PdfPixel.Files;

/// <summary>
/// The document-level embedded files (/Names /EmbeddedFiles).
/// </summary>
public sealed class PdfEmbeddedFiles
{
    private readonly IPdfDocument _document;

    private PdfEmbeddedFiles(IPdfDocument document, Dictionary<PdfString, PdfFileSpecification> items)
    {
        _document = document;
        Items = items;
    }

    /// <summary>
    /// Embedded files keyed by their name. Empty if the name tree has none.
    /// </summary>
    public IReadOnlyDictionary<PdfString, PdfFileSpecification> Items { get; }

    /// <summary>
    /// Returns the file of a child step of an embedded go-to path in this document: the embedded file with its name,
    /// or the file of the file attachment annotation it names; <see langword="null"/> when the step is not a child
    /// step or the file is not found.
    /// </summary>
    /// <param name="step">The path step.</param>
    public PdfFileSpecification? ResolveFile(PdfEmbeddedPathStep step)
    {
        if (step == null)
        {
            throw new ArgumentNullException(nameof(step));
        }

        if (step.Direction != PdfEmbeddedPathDirection.Child)
        {
            return null;
        }

        if (step.EmbeddedFileName != null)
        {
            return (Items.TryGetValue(step.EmbeddedFileName.Value, out PdfFileSpecification? file)) ? file : null;
        }

        IPdfPage? page = ResolveAnnotationPage(step);
        if (page == null)
        {
            return null;
        }

        PdfAnnotationBase? annotation = ResolveAnnotation(page, step);

        return (annotation as PdfFileAttachmentAnnotation)?.FileSpecification;
    }

    /// <summary>
    /// The embedded files of the catalog's /Names /EmbeddedFiles entry, or <see langword="null"/> when the catalog has none.
    /// </summary>
    internal static PdfEmbeddedFiles? FromCatalog(PdfDictionary catalog, PdfTreeReader treeReader)
    {
        PdfDictionary? embeddedFiles = catalog.GetDictionary(PdfTokens.NamesKey)?.GetDictionary(PdfTokens.EmbeddedFilesKey);
        if (embeddedFiles == null)
        {
            return null;
        }

        return new PdfEmbeddedFiles(catalog.Document, treeReader.ReadNameTree(embeddedFiles, PdfFileSpecification.GetFileSpecification));
    }

    private IPdfPage? ResolveAnnotationPage(PdfEmbeddedPathStep step)
    {
        int? pageIndex = step.AnnotationPageIndex;

        if (pageIndex == null && step.AnnotationPageDestination != null)
        {
            PdfNamedDestination destination = new(step.AnnotationPageDestination.Value);
            pageIndex = _document.Destinations.Resolve(destination)?.PageIndex;
        }

        if (pageIndex == null || pageIndex.Value < 0 || pageIndex.Value >= _document.Pages.Count)
        {
            return null;
        }

        return _document.Pages[pageIndex.Value];
    }

    private static PdfAnnotationBase? ResolveAnnotation(IPdfPage page, PdfEmbeddedPathStep step)
    {
        IReadOnlyList<PdfPageAnnotation> annotations = page.GetAnnotations();

        if (step.AnnotationIndex != null)
        {
            int index = step.AnnotationIndex.Value;
            return (index >= 0 && index < annotations.Count) ? annotations[index].Content : null;
        }

        if (step.AnnotationName != null)
        {
            foreach (PdfPageAnnotation annotation in annotations)
            {
                if (annotation.Content.Name != null && annotation.Content.Name.Value.Equals(step.AnnotationName.Value))
                {
                    return annotation.Content;
                }
            }
        }

        return null;
    }
}
