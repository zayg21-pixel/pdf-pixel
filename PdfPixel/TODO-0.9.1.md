# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores, ordered by effort. Spec references are ISO 32000-2:2020 tables
unless noted otherwise.

## 1. Easy wins

Single values or small dictionaries, exposed as-is on existing types.

### Document and page information (agreed design)

New types:

| Type | Kind | Members | Spec |
|---|---|---|---|
| `PdfVersion` | readonly struct | `Major`, `Minor`; parsed from `%PDF-x.y` and from a `/Version` name such as `/2.0` | 7.5.2, Table 29 |
| `PdfFileIdentifier` | readonly struct | `Permanent`, `Changing` (`PdfString`) | Tables 15, 43 |
| `PdfDocumentInformation` | sealed class | `Title`, `Author`, `Subject`, `Keywords`, `Creator`, `Producer` (`PdfString?`); `CreationDate`, `ModificationDate` (`DateTime?`); `IsTrapped` (`bool?`: `/True`, `/False`, null for `/Unknown` or absent); `CustomEntries` (`IReadOnlyDictionary<PdfString, PdfString>`, other keys are text strings per 14.3.3) | Table 349 |
| `PdfDeveloperExtension` | sealed class | `BaseVersion` (`PdfVersion?`), `ExtensionLevel` (`int?`), `Url` (`PdfString?`), `ExtensionRevision` (`PdfString?`) | Table 49 |
| `PdfMarkInformation` | sealed class | `Marked`, `UserProperties`, `Suspects` (`bool?`) | Table 353 |
| `PdfMetadata` | sealed class | `Reference`, `Stream`, `GetData()`; raw XMP, no XML parsing | Table 347 |
| `PdfPageMode` | `[PdfEnum]` | `UseNone`, `UseOutlines`, `UseThumbs`, `FullScreen`, `UseOC`, `UseAttachments`, `Unknown` | Table 29 |
| `PdfPageLayout` | `[PdfEnum]` | `SinglePage`, `OneColumn`, `TwoColumnLeft`, `TwoColumnRight`, `TwoPageLeft`, `TwoPageRight`, `Unknown` | Table 29 |
| `PdfTabOrder` | `[PdfEnum]` | `Row` (R), `Column` (C), `Structure` (S), `AnnotationsArray` (A), `Widget` (W), `Unknown` | Table 31 |
| `PdfPermissions` | `[Flags]` | `Print` 1<<2, `Modify` 1<<3, `Copy` 1<<4, `Annotate` 1<<5, `FillForms` 1<<8, `Assemble` 1<<10, `HighQualityPrint` 1<<11 (bit 10 ignored per Table 22) | Tables 22, 24 |
| `PdfSignatureFlags` | `[Flags]` | `None` 0, `SignaturesExist` 1<<0, `AppendOnly` 1<<1 | Table 224 |

`IPdfDocument`:

| Property | Type | Source |
|---|---|---|
| `HeaderVersion` | `PdfVersion?` | `%PDF-` header |
| `Version` | `PdfVersion?` | Effective version: catalog `/Version` when later than the header (7.7.2) |
| `Extensions` | `IReadOnlyDictionary<PdfString, IReadOnlyList<PdfDeveloperExtension>>` | Catalog; prefix to dictionary or array (2.0) |
| `Information` | `PdfDocumentInformation?` | Trailer `/Info` |
| `Id` | `PdfFileIdentifier?` | Trailer `/ID` |
| `Lang` | `PdfString?` | Catalog |
| `MarkInformation` | `PdfMarkInformation?` | Catalog `/MarkInfo` |
| `BaseUri` | `PdfString?` | Catalog `/URI` `Base` (Table 211) |
| `PageMode`, `PageLayout` | `PdfPageMode?`, `PdfPageLayout?` | Catalog |
| `NeedsRendering` | `bool?` | Catalog |
| `AssociatedFiles` | `IReadOnlyList<PdfFileSpecification>?` | Catalog `/AF` |
| `Metadata` | `PdfMetadata?` | Catalog |
| `Permissions` | `PdfPermissions?` | Encryption `/P`; null when not encrypted |
| `SignatureFlags` | `PdfSignatureFlags` | AcroForm `/SigFlags`; default `None` |

`IPdfPage`:

| Property | Type | Source |
|---|---|---|
| `BleedBox`, `TrimBox`, `ArtBox` | `PdfRectangle` | Default CropBox, intersected with MediaBox (14.11.2) |
| `UserUnit` | `float` | Default 1.0 |
| `StructParents` | `int?` | Read internally today |
| `LastModified` | `DateTime?` | |
| `Metadata` | `PdfMetadata?` | |
| `AssociatedFiles` | `IReadOnlyList<PdfFileSpecification>?` | `/AF` |
| `Duration` | `float?` | `/Dur`; null means no auto-advance |
| `Tabs` | `PdfTabOrder?` | |
| `TemplateInstantiated` | `PdfString?` | |
| `Id`, `PreferredZoom` | `PdfString?`, `float?` | Web capture `/ID`, `/PZ` |

Migration: `PdfFileSpecification.Id` becomes `PdfFileIdentifier?`.

### Remaining

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/Mac` | Embedded file `/Params` | Table 45 | Mac OS Subtype, Creator, ResFork (deprecated in 2.0) |

## 2. Model additions

New typed models with several fields or enums. Pure additions, no effect on rendering.

| Entry | Location | Spec | Contents |
|---|---|---|---|
| `/ViewerPreferences` | Catalog | Table 147 | Window: HideToolbar, HideMenubar, HideWindowUI, FitWindow, CenterWindow, DisplayDocTitle. Presentation: NonFullScreenPageMode, Direction. Print: PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies. Deprecated (2.0): ViewArea, ViewClip, PrintArea, PrintClip. Other: Enforce |
| `/OpenAction` | Catalog | Table 29 | Initial destination (page + zoom), or action run on open (e.g. JavaScript `print()`, Named `/Print`) |
| `/AA` | Catalog | Table 29 | Event actions: WC (will close), WS (will save), DS (did save), WP (will print), DP (did print) |
| `/AA` | Page | Table 31 | O (page opened), C (page closed) actions |
| `/Trans` | Page | Table 164 | Presentation transition effect (Split, Blinds, Box, Wipe, Dissolve, Glitter, Fly, Push, Cover, Uncover, Fade) |
| `/OutputIntents` | Catalog, Page (2.0) | Tables 401, 402 | S (GTS_PDFX, GTS_PDFA1, ISO_PDFE1), OutputCondition, OutputConditionIdentifier, RegistryName, Info, DestOutputProfile, DestOutputProfileRef, MixingHints, SpectralData. Today only the first usable catalog ICC profile is used internally |
| `/OCProperties` | Catalog | Tables 98, 99 | `/D` and `/Configs` configuration dictionaries: Name, Creator, BaseState, ON, OFF, Intent, AS, Order (tree with text labels, kept nested instead of flattened), ListMode, RBGroups, Locked |
| `/Intent`, `/Usage` | Optional content group | Tables 96, 100 | Intent (View, Design, extensible). Usage: CreatorInfo, Language, Export, Zoom, Print, View, User, PageElement |
| `/AS` | OC configuration | Table 101 | Usage application dictionaries: Event (View, Print, Export), OCGs, Category |
| `/BoxColorInfo` | Page | Tables 396, 397 | Display colors and styles for page boundaries |
| `/SeparationInfo` | Page | Table 400 | Separation page information |
| `/PieceInfo` | Catalog, Page, Form XObject | Table 350 | Application private data |
| `/AF`, `/Metadata` | Image and form XObjects | Tables 87, 93, 14.13.7 | Associated files and XMP metadata of an XObject |
| `/Collection` | Catalog | Tables 153–158, 46, 47 | Portfolio, together with the file specification `/CI` collection items |
| `/Requirements` | Catalog | Tables 273–276 | Features a viewer must support |
| `/Legal` | Catalog | Table 264 | Legal attestation |
| `/DSS` | Catalog | Table 261 | Document security store (signature validation data) |
| `/SpiderInfo` | Catalog | Table 386 | Web capture information |

## 3. Complex

Trees, cross-references between objects, or changes to rendering and text extraction.

| Entry | Location | Spec | Work |
|---|---|---|---|
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

## 4. Public API

| Item | Location | Work |
|---|---|---|
| Image conversion | `Imaging/Model/PdfImage.cs` | No public way to get pixels from a `PdfImage` (`/Thumb`, file specification thumbnails, image XObjects): decoding needs an internal `ImageDecodingContext`, and `Skia/Converters/PdfImageConverter.cs` is internal |

## 5. Refactoring

| Item | Location | Work |
|---|---|---|
| Object cache | `Models/PdfDocumentObjectCache.cs` | Use a single `PdfReference` cache, including `FileSpecifications` and `EmbeddedFiles`; cache other big shared objects, such as CMaps |

## 6. Bugs

| Bug | Location | Spec | Wrong output |
|---|---|---|---|
| Non-inheritable page attributes inherited | `Models/PdfPageResources.cs` | 7.7.3.4, Table 31 | BleedBox, TrimBox, ArtBox and `/Annots` are taken from `/Pages` nodes; only Resources, MediaBox, CropBox and Rotate are inheritable. Annotations on an intermediate node appear on every page below it |
| `/UserUnit` ignored | Page | Table 31, 8.3.2.3 | Pages with a user unit other than 1 get the wrong physical size |
| Page `/OutputIntents` ignored | `Parsing/PdfOutputIntentParser.cs` | 14.11.5 | When output intents are respected, a page-level output intent shall be used for that page; the catalog profile is used instead |
| Optional content `/Intent` ignored | `Parsing/PdfOptionalContentGroupParser.cs`, `Commands/Context/PdfMarkedContentState.cs` | 8.11.2.3, Table 99 | The default configuration's intent is View. A group whose intents do not include View shall have no effect on visibility; it is hidden today when listed in `/OFF`. Not yet checked against Acrobat |
| Optional content `/AS` View event ignored | `Parsing/PdfOptionalContentGroupParser.cs` | 8.11.4.4, Tables 100, 101 | Groups managed by a View usage application shall start in their `ViewState`; they keep the `/ON`/`/OFF` state today. Not yet checked against Acrobat |
| Soft mask text | `Transparency/Utilities/SoftMaskUtilities.cs` | | Text shown by the mask form is extracted like page text, in both rendering and text extraction |
