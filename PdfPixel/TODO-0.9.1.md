# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores, ordered by effort. Spec references are ISO 32000-2:2020 tables
unless noted otherwise.

## 1. Easy wins

Single values or small dictionaries, exposed as-is on existing types.

| Entry | Location | Spec | Contents |
|---|---|---|---|
| Header version | `%PDF-1.x` | 7.5.2 | Version the file declares |
| `/Version` | Catalog | Table 29 | Overrides the header version (incremental updates) |
| `/Extensions` | Catalog | Tables 48, 49 | Developer extension levels, e.g. `/ADBE` `BaseVersion` + `ExtensionLevel` |
| `/Info` | Trailer | Table 349 | Title, Author, Subject, Keywords, Creator, Producer, CreationDate, ModDate, Trapped |
| `/ID` | Trailer | Table 15 | File identifier pair (read today only for decryption) |
| `/Lang` | Catalog | Table 29 | Document language |
| `/MarkInfo` | Catalog | Table 353 | Marked, UserProperties, Suspects |
| `/URI` | Catalog | Table 211 | `Base` URI for relative links |
| `/PageMode` | Catalog | Table 29 | UseNone, UseOutlines, UseThumbs, FullScreen, UseOC, UseAttachments |
| `/PageLayout` | Catalog | Table 29 | SinglePage, OneColumn, TwoColumnLeft/Right, TwoPageLeft/Right |
| `/NeedsRendering` | Catalog | Table 29 | XFA form must be rendered by the viewer |
| `/AF` | Catalog | Table 29, 14.13.2 | Associated files (`PdfFileSpecification.FromArray`) |
| `/Metadata` | Catalog | Table 29 | XMP metadata stream |
| `/P` | Encryption | Tables 22, 24 | User access permissions: print, modify, copy, annotate, fill forms, assemble, high-quality print. Read today into `PdfDecryptorParameters.Permissions`, not exposed |
| `/SigFlags` | AcroForm | Table 224 | SignaturesExist, AppendOnly |
| `/BleedBox`, `/TrimBox`, `/ArtBox` | Page | Table 31 | Resolved in `PdfPageResources`, not exposed on `IPdfPage` |
| `/StructParents` | Page | Table 31 | Key into the structure parent tree (read internally, not exposed on `IPdfPage`) |
| `/LastModified` | Page | Table 31 | Modification date |
| `/Metadata` | Page | Table 31 | Page XMP metadata stream |
| `/AF` | Page | Table 31, 14.13.4 | Associated files (`PdfFileSpecification.FromArray`) |
| `/Dur` | Page | Table 31 | Seconds the page is shown before auto-advancing in presentation mode |
| `/Tabs` | Page | Table 31 | Annotation tab order: R, C, S, A, W |
| `/TemplateInstantiated` | Page | Table 31 | Named page created from a template |
| `/ID`, `/PZ` | Page | Table 31 | Web capture identifier and preferred zoom |
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
| `/UserUnit` ignored | Page | Table 31, 8.3.2.3 | Pages with a user unit other than 1 get the wrong physical size |
| Page `/OutputIntents` ignored | `Parsing/PdfOutputIntentParser.cs` | 14.11.5 | When output intents are respected, a page-level output intent shall be used for that page; the catalog profile is used instead |
| Optional content `/Intent` ignored | `Parsing/PdfOptionalContentGroupParser.cs`, `Commands/Context/PdfMarkedContentState.cs` | 8.11.2.3, Table 99 | The default configuration's intent is View. A group whose intents do not include View shall have no effect on visibility; it is hidden today when listed in `/OFF`. Not yet checked against Acrobat |
| Optional content `/AS` View event ignored | `Parsing/PdfOptionalContentGroupParser.cs` | 8.11.4.4, Tables 100, 101 | Groups managed by a View usage application shall start in their `ViewState`; they keep the `/ON`/`/OFF` state today. Not yet checked against Acrobat |
| Soft mask text | `Transparency/Utilities/SoftMaskUtilities.cs` | | Text shown by the mask form is extracted like page text, in both rendering and text extraction |
