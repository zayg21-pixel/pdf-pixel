# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores, ordered by effort. Spec references are ISO 32000-2:2020 tables
unless noted otherwise.

## 1. Easy wins

Single values or small dictionaries, exposed as-is on existing types.

### Remaining

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/Mac` | Embedded file `/Params` | Table 45 | Mac OS Subtype, Creator, ResFork (deprecated in 2.0) |

## 2. Model rework

Models that replace or restructure an existing API.

### Document

| Entry | Location | Spec | Work |
|---|---|---|---|
| `/OpenAction` | Catalog | Table 29 | Initial destination (page + zoom), or action run on open (e.g. JavaScript `print()`, Named `/Print`) |
| `/OutputIntents` | Catalog | Tables 401, 402 | S (GTS_PDFX, GTS_PDFA1, ISO_PDFE1), OutputCondition, OutputConditionIdentifier, RegistryName, Info, DestOutputProfile, DestOutputProfileRef, MixingHints, SpectralData. Today only the first usable catalog ICC profile is used internally |

### Optional content (agreed design)

Model follows the PDF shape (8.11, Tables 96–101); content-side `/OC` keeps pointing to an OCG or OCMD.

| Type | Members |
|---|---|
| `PdfOptionalContentProperties` (`IPdfDocument.OptionalContentProperties`, replaces `OptionalContentGroups`) | `Groups` (`/OCGs`, by reference), `DefaultConfiguration` (`/D`), `Configurations` (`/Configs`) |
| `PdfOptionalContentGroup` | `Reference`, `Name`, `Intent` (`IReadOnlyList<PdfOptionalContentIntent>?`), `Usage`; no visibility state |
| `PdfOptionalContentUsage` | Table 100 flattened: `Creator`, `CreatorSubtype` (+ `RawCreatorSubtype`), `Lang`, `IsLanguagePreferred`, `IsExportOn`, `ZoomMin`, `ZoomMax`, `PrintSubtype` (+ `RawPrintSubtype`), `IsPrintOn`, `IsViewOn`, `UserType`, `UserNames`, `PageElement` |
| `IPdfOptionalContentConfiguration` | Table 99, resolved (`Order`/`RBGroups` inherited from `/D`): `Name`, `Creator`, `BaseState`, `On`, `Off`, `Intent`, `AutoState`, `Order` (tree: `Label`, `Group`, `Children`), `ListMode`, `RadioButtonGroups`, `Locked`; context: `Event`, `Zoom`, `Language`, `User` |
| `PdfOptionalContentConfiguration` | Sealed, immutable, parsed from `/D` and `/Configs`; context members null; `ToUserConfiguration()` |
| `PdfUserOptionalContentConfiguration` | Sealed, mutable; same members, collections edited in place (`On`/`Off`/`Locked` sets); copy constructor from `IPdfOptionalContentConfiguration` |
| `PdfOptionalContentUsageApplication` | `/AS` entry: `Event`, `Groups`, `Category` |
| `PdfOptionalContentMembership` | OCG or OCMD: `Type`, `Groups`, `VisibilityPolicy` (default `AnyOn`), `VisibilityExpression` |
| Value types | `PdfOptionalContentIntent` (`Type`: `Raw`, `View`, `Design`, `All`; `RawType`), `PdfOptionalContentBaseState`, `PdfOptionalContentListMode`, `PdfOptionalContentEvent`, `PdfOptionalContentUsageCategory` |

Rendering takes one `IPdfOptionalContentConfiguration`. Context defaults when applied: `Event` View, `Zoom` 1, `Language` document `Lang` falling back to `en`; null `User` skips the User category. Evaluation: `BaseState`, then `On`/`Off`, then `/AS` entries matching `Event`; groups whose intents do not match `Intent` have no effect (8.11.2.3). Fixes the `/Intent` and `/AS` bugs.

### Page

| Entry | Location | Spec | Work |
|---|---|---|---|
| `/OutputIntents` | Page (2.0) | Tables 401, 402 | S (GTS_PDFX, GTS_PDFA1, ISO_PDFE1), OutputCondition, OutputConditionIdentifier, RegistryName, Info, DestOutputProfile, DestOutputProfileRef, MixingHints, SpectralData. Today only the first usable catalog ICC profile is used internally |

## 3. Model additions

New typed models on an existing owner. Catalog entries land on `IPdfDocument`; nested dictionaries keep the PDF object hierarchy as their own types.

### Document

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/ViewerPreferences` | Catalog | Table 147 | Window: HideToolbar, HideMenubar, HideWindowUI, FitWindow, CenterWindow, DisplayDocTitle. Presentation: NonFullScreenPageMode, Direction. Print: PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies. Deprecated (2.0): ViewArea, ViewClip, PrintArea, PrintClip. Other: Enforce |
| Additional actions (`/AA`) | Catalog | Tables 29, 200 | Trigger-event actions: WillClose (`WC`), WillSave (`WS`), DidSave (`DS`), WillPrint (`WP`), DidPrint (`DP`) |
| `/PieceInfo` | Catalog | Table 350 | Application private data |
| `/Requirements` | Catalog | Tables 273–276 | Features a viewer must support |
| `/Legal` | Catalog | Table 264 | Legal attestation |
| `/DSS` | Catalog | Table 261 | Document security store (signature validation data) |
| `/SpiderInfo` | Catalog | Table 386 | Web capture information |
| `/Collection` | Catalog | Tables 153–158, 46, 47 | Portfolio, together with the file specification `/CI` collection items |

### Page

| Entry | Location | Spec | Contents |
|---|---|---|---|
| Additional actions (`/AA`) | Page | Tables 31, 198 | Trigger-event actions: PageOpened (`O`), PageClosed (`C`) |
| `/Trans` | Page | Table 164 | Presentation transition effect (Split, Blinds, Box, Wipe, Dissolve, Glitter, Fly, Push, Cover, Uncover, Fade) |
| `/BoxColorInfo` | Page | Tables 396, 397 | Display colors and styles for page boundaries |
| `/SeparationInfo` | Page | Table 400 | Separation page information |
| `/PieceInfo` | Page | Table 350 | Application private data |

## 4. Complex

Trees, cross-references between objects, or changes to rendering and text extraction.

| Entry | Location | Spec | Work |
|---|---|---|---|
| `/AF`, `/Metadata`, `/PieceInfo` | Image and form XObjects | Tables 87, 93, 350, 14.13.7 | Associated files and XMP metadata of an XObject, page-piece data of a form XObject; XObjects are only created during rendering, so this needs a public access path |
| `/Outlines` | Catalog | Tables 150, 151 | Bookmark tree |
| `/Threads`, `/B` | Catalog, Page | Tables 162, 163 | Article threads and the beads on each page |
| `/Names` | Catalog | Table 32 | Everything except `/Dests` and `/EmbeddedFiles`: JavaScript, AP, Pages, Templates, IDS, URLS, AlternatePresentations, Renditions |
| `/DPartRoot`, `/DPart` | Catalog, Page | Tables 408, 409 | Document parts hierarchy, its `/AF` and `/Metadata` |
| `/PresSteps` | Page | Table 165 | Sub-page navigation steps |
| `/VP` | Page | Tables 265, 266 | Viewports (measurement, geospatial) |
| `/Measure`, `/PtData` | Image and form XObjects | Tables 87, 93, 266, 272 | Measurement and point data |
| `/Perms` | Catalog | Table 263 | DocMDP / UR3 permission signatures |
| `/AcroForm` | Catalog | Table 224 | NeedAppearances, DR, DA, Q — affects rendering of widgets without appearance streams |
| `/Alternates` | Image XObject | Table 89 | Alternate images; selection by OC and DefaultForPrinting (8.9.5.4 algorithm) |
| `/UseBlackPtComp` | ExtGState | Table 57, PDF 2.0 AN001 | Black point compensation: ON, OFF, Default |
| AESV4 crypt filter | Encryption | ISO/TS 32003 | AES-GCM encryption; such files cannot be opened today |
| Integrity protection | Encryption | ISO/TS 32004 | `KDFSalt` and document MAC |

### Marked content and text extraction

| Entry | Location | Spec | Work |
|---|---|---|---|
| Artifact property list | `Artifact` BDC properties | 14.8.2.2 | Type (Pagination, Layout, Page, Background), Subtype, BBox, Attached (Top, Bottom, Left, Right) |
| `/Alt`, `/E` | `Span` and `Artifact` BDC properties | 14.9.3, 14.9.5 | Alternate description and expansion, falling back to the MCID's structure element like `/ActualText` |
| `/ActualText` over non-text content | `TextExtraction/PdfTextBlockFlattener.cs` | 14.9.4 | Replacement text of a block drawn with paths or images is dropped: its bounds come from characters only |
| `AF` marked content | BDC with tag `AF` | 14.13.5 | Associated files of a content section; the property list is an array of file specifications |

## 5. Public API

| Item | Location | Work |
|---|---|---|
| Image conversion | `Imaging/Model/PdfImage.cs` | No public way to get pixels from a `PdfImage` (`/Thumb`, file specification thumbnails, image XObjects): decoding needs an internal `ImageDecodingContext`, and `Skia/Converters/PdfImageConverter.cs` is internal |

## 6. Refactoring

| Item | Location | Work |
|---|---|---|
| Object cache | `Models/PdfDocumentObjectCache.cs` | Use a single `PdfReference` cache, including `FileSpecifications` and `EmbeddedFiles`; cache other big shared objects, such as CMaps |
| Content locker | `PdfPixel.PdfPanel`, `PdfCommandExecutionContext.ContentLocker` | Investigate document access safety: lazily loaded content read from the UI thread can corrupt content read on another thread. Make access safer by exposing the locker through the panel's page collection |

## 7. Bugs

| Bug | Location | Spec | Wrong output |
|---|---|---|---|
| Non-inheritable page attributes inherited | `Models/PdfPageResources.cs` | 7.7.3.4, Table 31 | BleedBox, TrimBox, ArtBox and `/Annots` are taken from `/Pages` nodes; only Resources, MediaBox, CropBox and Rotate are inheritable. Annotations on an intermediate node appear on every page below it |
| `/UserUnit` ignored | Page | Table 31, 8.3.2.3 | Pages with a user unit other than 1 get the wrong physical size; the value is exposed as `IPdfPage.UserUnit` but not applied |
| Page `/OutputIntents` ignored | `Parsing/PdfOutputIntentParser.cs` | 14.11.5 | When output intents are respected, a page-level output intent shall be used for that page; the catalog profile is used instead |
| OCMD `/P` default | `Models/PdfOptionalContentVisibilityPolicy.cs` | Table 97 | Absent `/P` is read as `AllOn`; the default is `AnyOn` |
| Soft mask text | `Transparency/Utilities/SoftMaskUtilities.cs` | | Text shown by the mask form is extracted like page text, in both rendering and text extraction |
