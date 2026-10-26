using PdfPixel.Files;
using PdfPixel.Objects;
using PdfPixel.Parsing;
using System;
using System.Collections.Generic;

namespace PdfPixel.PdfPanel.Mvvm;

/// <summary>
/// Bindable files embedded in the document and attached to the document or its pages.
/// </summary>
public sealed class PdfAttachmentsViewModel
{
    /// <summary>
    /// Initializes a view model over the embedded files of <paramref name="panelDocument"/>.
    /// </summary>
    /// <param name="panelDocument">The panel's document.</param>
    public PdfAttachmentsViewModel(PdfPanelDocument panelDocument)
    {
        if (panelDocument == null)
        {
            throw new ArgumentNullException(nameof(panelDocument));
        }

        IPdfDocument document = panelDocument.Document;
        List<PdfAttachmentViewModel> items = [];
        HashSet<PdfReference> documentReferences = [];

        AddDocumentItems(items, documentReferences, document.EmbeddedFiles.Items?.Values);
        AddDocumentItems(items, documentReferences, document.AssociatedFiles);

        foreach (IPdfPage page in document.Pages)
        {
            if (page.AssociatedFiles == null)
            {
                continue;
            }

            foreach (PdfFileSpecification fileSpecification in page.AssociatedFiles)
            {
                if (IsEmbedded(fileSpecification))
                {
                    items.Add(new PdfAttachmentViewModel(fileSpecification, page.PageNumber));
                }
            }
        }

        Items = items;
    }

    /// <summary>
    /// Attached files, document files first, then page files in page order; empty when the document has none.
    /// </summary>
    public IReadOnlyList<PdfAttachmentViewModel> Items { get; }

    /// <summary>
    /// Whether the document has attached files.
    /// </summary>
    public bool HasItems => Items.Count > 0;

    private static void AddDocumentItems(
        List<PdfAttachmentViewModel> items,
        HashSet<PdfReference> documentReferences,
        IEnumerable<PdfFileSpecification>? fileSpecifications)
    {
        if (fileSpecifications == null)
        {
            return;
        }

        foreach (PdfFileSpecification fileSpecification in fileSpecifications)
        {
            if (!IsEmbedded(fileSpecification))
            {
                continue;
            }

            if (fileSpecification.Reference != null && !documentReferences.Add(fileSpecification.Reference.Value))
            {
                continue;
            }

            items.Add(new PdfAttachmentViewModel(fileSpecification, null));
        }
    }

    private static bool IsEmbedded(PdfFileSpecification fileSpecification)
        => fileSpecification.EmbeddedFile != null || fileSpecification.UnicodeEmbeddedFile != null;
}
