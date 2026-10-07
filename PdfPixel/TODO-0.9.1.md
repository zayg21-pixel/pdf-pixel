# 0.9.1 — Missing document and page level entries

Entries the reader currently ignores. Split into **information** (data to expose, no effect on
what a viewer does) and **behavior** (instructions to a viewer: how to open, show or print).

## Document — information

| Entry | Location | Contents |
|---|---|---|
| Header version | `%PDF-1.x` | Version the file declares |
| `/Version` | Catalog | Overrides the header version (incremental updates) |
| `/Extensions` | Catalog | Developer extension levels, e.g. `/ADBE` `BaseVersion` + `ExtensionLevel` |
| `/Info` | Trailer | Title, Author, Subject, Keywords, Creator, Producer, CreationDate, ModDate, Trapped |
| `/Metadata` | Catalog | XMP metadata stream |
| `/ID` | Trailer | File identifier pair (read today only for decryption) |
| `/Lang` | Catalog | Document language |
| `/MarkInfo` | Catalog | Marked, UserProperties, Suspects |
| `/Outlines` | Catalog | Bookmark tree |
| `/Threads` | Catalog | Article threads |
| `/Names` | Catalog | Everything except `/Dests`: EmbeddedFiles, JavaScript, AP, Pages, Templates, IDS, URLS |
| `/AF` | Catalog | Associated files |
| `/Collection` | Catalog | Portfolio |
| `/Perms` | Catalog | DocMDP / UR3 permission signatures |
| `/Legal` | Catalog | Legal attestation |
| `/Requirements` | Catalog | Features a viewer must support |
| `/DSS` | Catalog | Document security store (signature validation data) |
| `/DPartRoot` | Catalog | Document parts hierarchy |
| `/PieceInfo` | Catalog | Application private data |
| `/SpiderInfo` | Catalog | Web capture information |

## Document — behavior

| Entry | Location | Controls |
|---|---|---|
| `/PageMode` | Catalog | UseNone, UseOutlines, UseThumbs, FullScreen, UseOC, UseAttachments |
| `/PageLayout` | Catalog | SinglePage, OneColumn, TwoColumnLeft/Right, TwoPageLeft/Right |
| `/OpenAction` | Catalog | Initial destination (page + zoom), or action run on open (e.g. JavaScript `print()`, Named `/Print`) |
| `/AA` | Catalog | Event actions: WC (will close), WS (will save), DS (did save), WP (will print), DP (did print) |
| `/URI` | Catalog | Base URI for relative links |
| `/AcroForm` | Catalog | NeedAppearances, DR, DA, Q — affects rendering of widgets without appearance streams |
| `/NeedsRendering` | Catalog | XFA form must be rendered by the viewer |

### `/ViewerPreferences` (catalog)

| Group | Keys |
|---|---|
| Window | HideToolbar, HideMenubar, HideWindowUI, FitWindow, CenterWindow, DisplayDocTitle |
| Presentation | NonFullScreenPageMode, Direction |
| Print | PrintScaling, Duplex, PickTrayByPDFSize, PrintPageRange, NumCopies |
| Deprecated (2.0) | ViewArea, ViewClip, PrintArea, PrintClip |
| Other | Enforce |

## Page — information

| Entry | Contents |
|---|---|
| `/UserUnit` | Size of a default user space unit (1/72 inch multiplier) — affects page size |
| `/StructParents` | Key into the structure parent tree |
| `/Thumb` | Thumbnail image |
| `/B` | Beads of article threads on the page |
| `/LastModified` | Modification date |
| `/Metadata` | Page XMP metadata stream |
| `/PieceInfo` | Application private data |
| `/BoxColorInfo` | Display colors for page boundaries |
| `/SeparationInfo` | Separation page information |
| `/ID`, `/PZ` | Web capture identifier and preferred zoom |
| `/VP` | Viewports (measurement, geospatial) |
| `/AF`, `/DPart` | Associated files, document part |

## Page — behavior

| Entry | Controls |
|---|---|
| `/Trans` | Presentation transition effect (Split, Blinds, Box, Wipe, Dissolve, Glitter, Fly, Push, Cover, Uncover, Fade) |
| `/Dur` | Seconds the page is shown before auto-advancing in presentation mode |
| `/AA` | O (page opened), C (page closed) actions |
| `/Tabs` | Annotation tab order: R, C, S, A, W |
| `/TemplateInstantiated` | Named page created from a template |
| `/PresSteps` | Sub-page navigation steps |
