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
| `/AF`, `/Metadata` | Image and form XObjects | Tables 87, 93, 14.13.7 | Associated files and XMP metadata of an XObject; XObjects are only created during rendering, so this needs a public access path |
| `/Outlines` | Catalog | Tables 150, 151 | Bookmark tree |
| `/Threads`, `/B` | Catalog, Page | Tables 162, 163 | Article threads and the beads on each page |
| `/PresSteps` | Page | Table 165 | Sub-page navigation steps |

### Marked content and text extraction

| Entry | Location | Spec | Work |
|---|---|---|---|
| `/ActualText` over non-text content | `TextExtraction/PdfTextBlockFlattener.cs` | 14.9.4 | Replacement text of a block drawn with paths or images is dropped: its bounds come from characters only |

## 3. Public API

| Item | Location | Work |
|---|---|---|
| Image conversion | `Imaging/Model/PdfImage.cs` | No public way to get pixels from a `PdfImage` (`/Thumb`, file specification thumbnails, image XObjects): decoding needs an internal `ImageDecodingContext`, and `Skia/Converters/PdfImageConverter.cs` is internal |

## 4. Refactoring

| Item | Location | Work |
|---|---|---|
| Object cache | `Models/PdfDocumentObjectCache.cs` | Use a single `PdfReference` cache, including `FileSpecifications` and `EmbeddedFiles`; cache other big shared objects, such as CMaps |
| Content locker | `PdfPixel.PdfPanel`, `PdfCommandExecutionContext.ContentLocker` | Investigate document access safety: lazily loaded content read from the UI thread can corrupt content read on another thread. Make access safer by exposing the locker through the panel's page collection |

## 5. Bugs

| Bug | Location | Spec | Wrong output |
|---|---|---|---|
| Non-inheritable page attributes inherited | `Models/PdfPageResources.cs` | 7.7.3.4, Table 31 | BleedBox, TrimBox, ArtBox and `/Annots` are taken from `/Pages` nodes; only Resources, MediaBox, CropBox and Rotate are inheritable. Annotations on an intermediate node appear on every page below it |
| `/UserUnit` ignored | Page | Table 31, 8.3.2.3 | Pages with a user unit other than 1 get the wrong physical size; the value is exposed as `IPdfPage.UserUnit` but not applied |
| Soft mask text | `Transparency/Utilities/SoftMaskUtilities.cs` | | Text shown by the mask form is extracted like page text, in both rendering and text extraction |
| Color space regression | Color spaces | | Wrong colors since the color space update. `issue20513.pdf`: the red and black logo renders washed out pink and gray. `issue6289.pdf` (GWG 18.1, 16-bit DeviceCMYK image): the whole page differs from the gold. Renders in `PdfPixel.Gold/Inspect` |
