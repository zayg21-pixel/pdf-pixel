# 0.9.1 — Missing document and page level entries

## 1. PdfPanel

| Entry | Location | Spec | Work |
|---|---|---|---|
| Thumbnails | `PdfPixel.PdfPanel/Mvvm` | 12.3.4 | No page thumbnails: a view model with an image per page, the page's `/Thumb` (`IPdfPage.Thumbnail`) when present, otherwise a low-resolution render, and navigation to the page on click; shown as a Thumbnails tab in the WPF demo side panel |
| `/PageMode` | `PdfPixel.Demo.Wpf` | Table 29 | Not honored: the side panel always starts hidden. The document's `IPdfDocument.PageMode` would open it on the matching tab when a document is loaded: Contents for `UseOutlines`, Layers for `UseOC`, Attachments for `UseAttachments`, Thumbnails for `UseThumbs` |
| Content interaction layer | `PdfPixel.PdfPanel` | 14.9.3, 14.9.4, 14.13 | No interaction with non-text content: a separate layer built from the content tree (`PdfContentTreeBuilder.Root`) would show `/ActualText` or `/Alt` of images and shadings in a popup on hover, and select an image on click so it can be copied (Ctrl+C). Files associated with content (`PdfContentScope.AssociatedFiles`: marked content and XObject `/AF`) are parsed but not shown: the layer would place an attachment icon over the content's bounds that saves or opens the file through the file API |
| `/UserUnit` | `PdfPixel.PdfPanel/PdfPanelPageInfo.cs` | Table 31, 8.3.2.3 | Not supported: pages are laid out in their own units, so pages with different user units keep the wrong relative size. Supporting it adds a per-page scale on top of the global zoom, which layout, rendering and screen-to-page mapping must apply. Page content is unaffected; the value is already exposed as `IPdfPage.UserUnit` |

## 2. File attachments

One way to reach every file of a document, and the panel features built on it.

| Item | Location | Spec | Work |
|---|---|---|---|
| File API | `Files` | 7.11, 14.13 | Partial: files are reachable as `PdfFileSpecification` with `PdfEmbeddedFile` from `IPdfDocument.EmbeddedFiles`, `/AF` of the document, pages, XObjects and structure elements, and `PdfFileAttachmentAnnotation.FileSpecification`, each in its own way. A single API exposes every file of the document the same way: name, description, MIME type, size, dates and content |
| Attachments | `PdfPixel.PdfPanel/Mvvm` | 7.11.4, 12.5.6.15 | No view of attached files: a view model listing the files of the document and its pages through the file API, with name, description, size and dates, and saving or opening the file; shown as an Attachments tab in the WPF demo side panel |
| Opening files | `PdfPixel.PdfPanel/Actions`, `PdfPixel.PdfPanel.Wpf` | 12.6.4.3 | Files the document asks to open are raised by `PdfPanelActions` as host events. `RemoteDocumentRequested` is already raised for GoToR, but `WpfPdfPanel` handles it with an empty handler; it needs to raise it as a WPF event for the host. Opening a file from the Attachments view model follows the same path |
| GoToE, Launch | `Actions/Model` | 12.6.4.4, 12.6.4.6 | Not modeled (`PdfGenericAction`); needs investigation |
| Portfolios | `Files` | 7.11.6, 12.3.5 | Not modeled: `/Collection` and collection items (`/CI`, see the TODO in `PdfFileSpecification`); needs investigation |

## 3. Web panel

Static document data is returned by `setDocument`; dynamic data arrives through the `setOnStateChanged` callback.

| Item | Location | Work |
|---|---|---|
| Document data | `PdfPixel.PdfPanel.Web` | `setDocument` returns only `{ ok, pageCount }` on success. It returns the document data instead: page count and static per-page info (label, crop box). Page labels leave the `info` of a frame page, which keeps only what places the page (page number, position, current rotation); `pageCount` leaves the view state |
| Permissions | `PdfPixel.PdfPanel.Web` | `IPdfDocument.Permissions` is not reachable from JS; part of the document data |
| `/PageMode` | `PdfPixel.PdfPanel.Web` | `IPdfDocument.PageMode` is not reachable from JS; part of the document data |
| Outlines | `PdfPixel.PdfPanel.Web` | No outline tree in JS: the tree is part of the document data, and activating an item goes through `PdfPanelActions.ActivateOutlineItem` |
| Optional content | `PdfPixel.PdfPanel.Web` | No layers in JS: the layer tree is part of the document data, and layer visibility is dynamic data with its own setter |
