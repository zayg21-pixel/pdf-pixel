# 0.9.1 — Missing document and page level entries

## 1. WPF demo

| Item | Location | Work |
|---|---|---|
| Demo refactor | `PdfPixel.Demo.Wpf` | Refactor the demo, including action support: handle `ActionRequested` (open URIs, open the files of GoToR and Launch actions, follow GoToE paths with `PdfEmbeddedFiles.ResolveFile` and navigate to the destination in the opened document) and open files from the Attachments tab |

## 2. Web panel

Static document data is returned by `setDocument`; dynamic data arrives through the `setOnStateChanged` callback.

| Item | Location | Work |
|---|---|---|
| Document data | `PdfPixel.PdfPanel.Web` | `setDocument` returns only `{ ok, pageCount }` on success. It returns the document data instead: page count and static per-page info (label, crop box). Page labels leave the `info` of a frame page, which keeps only what places the page (page number, position, current rotation); `pageCount` leaves the view state |
| Permissions | `PdfPixel.PdfPanel.Web` | `IPdfDocument.Permissions` is not reachable from JS; part of the document data |
| `/PageMode` | `PdfPixel.PdfPanel.Web` | `IPdfDocument.PageMode` is not reachable from JS; part of the document data |
| Outlines | `PdfPixel.PdfPanel.Web` | No outline tree in JS: the tree is part of the document data, and activating an item goes through `PdfPanelActions.ActivateOutlineItem` |
| Optional content | `PdfPixel.PdfPanel.Web` | No layers in JS: the layer tree is part of the document data, and layer visibility is dynamic data with its own setter |
| Attachments | `PdfPixel.PdfPanel.Web` | No attached files in JS: the list of `PdfAttachmentsViewModel` (name, description, page number) is part of the document data |
| Thumbnails | `PdfPixel.PdfPanel.Web` | No thumbnails in JS: `PdfPanelThumbnails` is not reachable. JS requests and cancels the thumbnail of a page, receives the PNG when it is ready, and activating a thumbnail navigates to its page |
| Code analysis | `PdfPixel.PdfPanel.Web` | Not analyzed: the project is not a package, so `Directory.Build.props` gives it none of the package settings. It needs the same `Nullable`, `AnalysisMode`, `EnforceCodeStyleInBuild` and Roslynator analyzers as the packages, with the resulting warnings fixed |
