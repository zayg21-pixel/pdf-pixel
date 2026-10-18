# 0.9.1 — Missing document and page level entries

## 1. Web panel

Static document data is returned by `setDocument`; dynamic data arrives through the `setOnStateChanged` callback.

| Item | Location | Work |
|---|---|---|
| Document data | `PdfPixel.PdfPanel.Web` | `setDocument` returns only `{ ok, pageCount }` on success. It returns the document data instead: page count and static per-page info (label, crop box). Page labels leave the `info` of a frame page, which keeps only what places the page (page number, position, current rotation); `pageCount` leaves the view state |
| Permissions | `PdfPixel.PdfPanel.Web` | `IPdfDocument.Permissions` is not reachable from JS; part of the document data |
| Outlines | `PdfPixel.PdfPanel.Web` | No outline tree in JS: the tree is part of the document data, and activating an item goes through `PdfPanelActions.ActivateOutlineItem` |
| Optional content | `PdfPixel.PdfPanel.Web` | No layers in JS: the layer tree is part of the document data, and layer visibility is dynamic data with its own setter |
| Attachments | `PdfPixel.PdfPanel.Web` | No attached files in JS: the list of `PdfAttachmentsViewModel` (name, description, page number) is part of the document data |
| Thumbnails | `PdfPixel.PdfPanel.Web` | No thumbnails in JS: `PdfPanelThumbnails` is not reachable. JS requests and cancels the thumbnail of a page, receives the PNG when it is ready, and activating a thumbnail navigates to its page |
| Web demo UI | `PdfPixel.Demo.Web` | The web demo UI does not match the WPF demo: bring over its logic (side rail shown per available content, search bar with results, logs) as the web panel exposes the data |

## 2. Technical debt

| Item | Location | Work |
|---|---|---|
| Document init sequence | `PdfDocument`, `PdfXrefLoader`, `PdfXrefRecoveryScanner` | Clean up the loading sequence: header, xref chain, `/Root` resolution and the recovery fallback. The recovery scanner sets the root on its own, so every caller re-checks the root after a lookup to let the scanner win. Decide where and when the root is resolved and whether the scan applied, in one place |
| General | All projects | Track public API changes
