# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores, ordered by effort. Spec references are ISO 32000-2:2020 tables
unless noted otherwise.

## 1. Model additions

New typed models on an existing owner. Catalog entries land on `IPdfDocument`; nested dictionaries keep the PDF object hierarchy as their own types.

### Document

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/ViewerPreferences` | Catalog | Table 147 | Window: HideToolbar, HideMenubar, HideWindowUI, FitWindow, CenterWindow, DisplayDocTitle. Presentation: NonFullScreenPageMode, Direction. Print: PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies. Deprecated (2.0): ViewArea, ViewClip, PrintArea, PrintClip. Other: Enforce |

### Page

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/Trans` | Page | Table 164 | Presentation transition effect (Split, Blinds, Box, Wipe, Dissolve, Glitter, Fly, Push, Cover, Uncover, Fade) |
| `/BoxColorInfo` | Page | Tables 396, 397 | Display colors and styles for page boundaries |

## 2. Complex

Trees, cross-references between objects, or changes to rendering and text extraction.

| Entry | Location | Spec | Work |
|---|---|---|---|
| `/Threads`, `/B` | Catalog, Page | Tables 162, 163 | Article threads and the beads on each page |
| `/PresSteps` | Page | Table 165 | Sub-page navigation steps |

### PdfPanel

| Entry | Location | Spec | Work |
|---|---|---|---|
| Content interaction layer | `PdfPixel.PdfPanel` | 14.9.3, 14.9.4 | No interaction with non-text content: a separate layer built from the content tree (`PdfContentTreeBuilder.Root`) would show `/ActualText` or `/Alt` of images and shadings in a popup on hover, and select an image on click so it can be copied (Ctrl+C) |
| `/UserUnit` | `PdfPixel.PdfPanel/PdfPanelPageInfo.cs` | Table 31, 8.3.2.3 | Not supported: pages are laid out in their own units, so pages with different user units keep the wrong relative size. Supporting it adds a per-page scale on top of the global zoom, which layout, rendering and screen-to-page mapping must apply. Page content is unaffected; the value is already exposed as `IPdfPage.UserUnit` |

## 3. Public API

| Item | Location | Work |
|---|---|---|
| Image conversion | `Imaging/Model/PdfImage.cs` | No public way to get pixels from a `PdfImage` (`/Thumb`, file specification thumbnails, image XObjects): decoding needs an internal `ImageDecodingContext`, and `Skia/Converters/PdfImageConverter.cs` is internal |
