# PdfPixel — Claude Code Guide

## Formatting

The project uses Roslynator with rules enforced as warnings. The linter rewrites code after every edit. Write code that matches these rules so the linter has nothing to change.

### Always-braces
Every `if`, `else`, `for`, `foreach`, `while` body needs braces — even single-line.
```csharp
// wrong
if (x == null) return;

// right
if (x == null)
{
    return;
}
```

### Expression bodies
Use `=>` for simple single-expression members. Use block body when the body spans multiple lines.
```csharp
public int Count => _items.Length;           // expression body ✓
public void Dispose() => _surface?.Dispose(); // expression body ✓
```

### var
Use `var` only when the type is obvious from the right-hand side.
```csharp
var surface = SKSurface.Create(info); // obvious ✓
SKSurface surface = GetSurface();     // not obvious, explicit type ✓
```

### Object creation
Omit the type when it's obvious from context (`new()` not `new SKPaint()`).
```csharp
SKPaint paint = new();       // ✓
using SKPaint paint = new(); // ✓
```

### Flags enum
Use bitwise operator, not `HasFlag`:
```csharp
if ((flags & PageDrawFlags.Shadow) != 0) // ✓
if (flags.HasFlag(PageDrawFlags.Shadow)) // ✗
```

Declare flag values with shift operators:
```csharp
Shadow     = 1 << 0,
Background = 1 << 1,
Content    = 1 << 2,
```

### Null checks
Use equality operator, not pattern matching:
```csharp
if (x == null) // ✓
if (x is null) // ✗
```

### Null-forgiving operator
Never use `!` (null-forgiving). Handle nullability explicitly with null checks, conditional access, or code restructuring.

### Nullable types — no fake fallbacks
When a value may be absent, use the nullable type (`SKRect?`, `int?`, etc.). Never substitute a sentinel value (`SKRect.Empty`, `0`, `string.Empty`) to represent "not present" — that destroys the distinction between absent and explicitly set to empty/zero.

### C# naming
Never use short or abbreviated variable names (`el`, `d`, `p`, `s`). Names must be descriptive.

### Wrapping existing APIs
Never wrap an existing framework method in a helper that only adds noise. Use the framework API directly.

### No task-tracking comments in code
Never add comments that reference the current task, what was changed, or instructions like "already updated, do not modify", "added for X", "do not touch". These belong in commit messages, not in code.

### Object initializers in arguments
Never inline object initializers (e.g. `new SKPaint { ... }`) directly inside method call arguments. Always assign to a named variable first — types like `SKPaint` have no constructor arguments so inlining makes the code unreadable.

### String empty
```csharp
string.Empty // ✓
""           // ✗
```

### Private fields
Always prefix with `_`:
```csharp
private readonly ILogger _logger;
```

### Delegates
Prefer method groups over lambdas:
```csharp
_syncContext.Post(OnPageUpdatedSync, null); // ✓ (when signature matches)
```

### Binary / arrow operators — newline before
```csharp
bool result = a
    && b
    && c;
```

### TODO comments
Never remove a `// TODO` comment when moving, copying, or editing code. TODOs are tracked future work — treat them as load-bearing. The only time a TODO may be removed is when the described work has actually been completed.

### Documentation comments
Multi-line summary style. Use `/// <inheritdoc />` for interface/override implementations — don't repeat the contract.

## Architecture notes

- PdfPixel.PdfPanel namespaces mirror the core library and never split public from internal: the root holds `PdfPanelContext`, `PdfPanelDocument`, `PdfPanelPage`, their extension methods and the execution observers; each context feature is its own namespace (`Actions`, `Annotations`, `Content`, `Input`, `Layers`, `Layout`, `Rendering`, `Text`, `Thumbnails`, plus `Mvvm`) with its internals inside; `Scheduling` (scheduler, work items, `IWorkQueue`) and `Caching` (page cache entries) are the job namespaces. The panel has no animations.
- Frame compositing always on the UI thread, with the `PdfPanelRenderer`'s own `CommandCache`; the workers share the document's, so no Snap cache is used by two threads. Typefaces are shared, so Snap draws text under the execution context's `ContentLocker`. Decoding and tile rasterization always through the work queue (`IWorkQueue`). Panel data may hold live model objects (`PdfImage`, annotations) — the old "plain data only across threads" rule is obsolete.
- Skia is gone from the core library's code: system fonts are substituted by `PdfFontSubstitutor` (font directories plus a fallback family, from `PdfFontSubstitutorFactory.Create(loggerFactory)` for the current operating system), which loads every face as an `SfntPdfTypeface` on first use, matches by its `Metrics`, checks characters with `GetGid`, and returns a new instance of the chosen face; Snap is the only content rasterizer (Gold and Diagnostics included). Snap lives in its own project, `PdfPixel.Snap`, depending on PdfPixel through its public API only (no `InternalsVisibleTo`); its pixel-grid rules are in the public `PdfSnapGrid`, of which only the snapping the panel uses is public; its entry point is `PdfSnapSurface` at the root, and each feature folder (`Rasterization`, `Clipping`, `Painting`, `Color`, `Transparency`, `Imaging`, `Shading`, `Pattern`, `Text`) is its own namespace. PdfPanel draws its frame on a Snap surface (`PdfPanelRenderer.FrameSurface`, DeviceRGB, the panel's size): background (an opaque rectangle fill of the whole surface, skipped when one page's paper covers the panel; the surface has no clear, a fully covered opaque Normal run is written without reading the pixels under it), rounded page clip, paper, thumbnail placeholder (only where the page's tiles do not cover what is visible, `PdfPageTiles.Covers`), cached tiles (`PdfImageSamplesDecoder` each, interpolated where enlarged like the thumbnail, opaque DeviceRGB without alpha: the tiler puts the paper under each tile's content when it copies the tile out) and the highlight layers as PDF recordings; the page shadow, drawn by `DrawPage` in page space before its clip and paper, is opaque axial edge and radial corner gradients whose stops (`PageShadow`, kept by the renderer per background color) follow a Gaussian falloff of the shadow color blended over the panel background, so it assumes an opaque background. The frame surface draws onto the render target's own samples (`IPdfPanelRenderTarget.GetFrameSamples`: RGB in any order, premultiplied alpha or none, converted run by run by `SnapLayerPixels`), created again when the target gives other samples, and the target presents them as they are (`Present`); user graphics draw onto the frame surface through `PdfPanelRenderer.OnPanelPageRendered` (page content coordinates, clipped to the page, after the page's content and the panel's page graphics, on every draw of the page, full or partial) and `OnPanelRendered` (panel pixels, after everything, clipped to what was just drawn: the whole panel, or the page outline of a partial page draw, so it is applied once per pixel), both with a `PdfSnapSurface` and a `PdfPanelFramePage`/`PdfPanelFrame` (size, `HostToPanel`, visible pages); MVVM sets them on `PdfPanelInterface`, which the WPF host copies to the renderer. Every pass ends with `IPdfPanelRenderTarget.Present(PdfPanelFrame)`, the same frame instance `OnPanelRendered` got. WPF presents its premultiplied BGRA samples into the `WriteableBitmap`; Web (`CanvasRenderTarget`, CPU only, no WebGL) presents premultiplied RGBA, which equals the canvas's unpremultiplied RGBA because frames are opaque, through one Emscripten call (`dotnet_set_canvas_rgba`: an `ImageData` over the heap bytes in place, `putImageData`, the 2D context kept per canvas), then reports the frame's JSON to JS; MAUI is out of date. The panel's update work item records page content and annotations into the cache entry's command recordings, then rasterizes the visible tiles with `PdfPageContentTiler` (owned by the scheduler, one Snap surface, created again with a new `PdfPageTilePool` when the request's `TileSize` changes) into the cache entry's `Tiles` (`PdfPageTiles`), and notifies `PageUpdated` once, when all visible tiles are done (`TilesUpdateResult.Updated`). The tiler reads the recordings and the `Tiles` locker from the page's cache entry itself. Each tile is drawn on the tiler's own Snap surface over transparent premultiplied 8-bit RGBA samples (cleared per tile, DeviceRGB): the page group (`IPdfPage.CreatePageGroup`, always isolated, `/Group` color space and knockout, otherwise the surface's color space) around the content recording and the annotation recording over it, drawn as its recording alone when the group declares DeviceRGB or no color space and no knockout, since the transparent root then is that group (`PdfPageRasterExtensions.GetRootContent`); the tile is then copied into its slot over white paper (`CopyOverPaper`, c + 255 − α, no float step). Thumbnails are drawn and copied the same way. Every tile is a square `TileSize × TileSize` RGB slot from the `PdfPageTilePool`, the whole tile surface exported into it; edge tiles reach past the page and are cut by the page clip when drawn. The pool's capacity comes from each request (`ContentUpdateRequest.TileCapacity`, set by `PdfPanelContext`: the tiles of a panel-sized region, one column and one row more than the panel spans, plus for each of the `LookAheadPageCount` pages the tiles of a square of 1.5 times the panel's larger side, the largest page looked ahead), set by the scheduler before eviction is queued: `Rent` allocates when it has no free slot, `Return` keeps a slot while rented and free slots together stay within the capacity, and a lower capacity drops the free slots beyond it at once. `PdfPageTiles` is a black box over its grid (`Grid`, `Initialize(grid)`, `GetTile(gridPoint)`, `Covers`) and the only one that rents and returns slots: `Initialize` returns the slots outside the new grid and rents empty ones for new points, `Dispose` returns them all; it is changed only through the locker (`GetContentForWriting` for `Initialize` and for writing a tile's pixels and recorded states, `SetContent` to replace or clear it). Each tile records the antialias, optional content states and annotation states it was rasterized with, all `null` while it is empty; empty tiles are not drawn. On the same grid, the stored set is initialized to the `VisibleRegion` in place, and empty tiles are rasterized, as is a tile whose antialias or optional content states differ from the request's or which overlaps an annotation whose state differs between its states and the request's (`PdfAnnotationExtensions.GetChangedAnnotationRegions`). A new scale or tile size starts a new set, and the old set stays drawn (scaled while zooming) until the new one is complete and stored; a cancelled pass on a new grid disposes its set, and on the same grid keeps the tiles it wrote, without notifying. Eviction of pages that left the request is queued before their replacements' updates, so their slots return to the pool first. Requests carry no refresh regions, so a skipped request loses nothing: the next pass compares the tiles with the newest request. No annotation picture exists anymore.
- Images: a `PdfImage` resolves its `/ColorSpace` at load (null for an image mask and for a JPX image that leaves it to its data; inline images resolve a name in the current resources, and the resolved space is part of `InlineImageKey`) and owns one decoder (`Decoder`). `DrawImageCommand` holds the image, the paint (the fill paint; for a non-mask image with the current resources' device mapping and the image's `/Intent`) and the group source; Snap takes the mask decoder from `SoftMask`/`Mask` by `MaskKind` and the sample color space from the decode info. Decoders get an `IPdfImageCache` (tile size; `Rent`, `Put`, `Find`, `Release` per image) in `SelectTiles`, which decodes whole images itself, and `ReleaseTiles` after each draw returns everything to it. Snap's is `PdfSnapImageCache` on `PdfSnapSurfaceParameters`: put data is kept within its put capacity (counted by the arrays it holds alive), least recently released images dropped first, and rented memory reused within its rent capacity; the panel's scheduler uses 32 MB / 32 MB, the frame renderer none.
- Page group: `IPdfPage.Render` emits the content commands only; `CreatePageGroup(DrawRecordingCommand)` wraps any recording (content plus annotations) in the page group. Gold, Diagnostics, Demo.Cli, the Examples, the tiler and the thumbnail rasterizer wrap; Gold, the tiler and the thumbnail rasterizer then draw only the recording when the group declares DeviceRGB or no color space and no knockout, as their transparent DeviceRGB root is that group; a caller that does not want page-group blending can skip it. Content and word extraction do not wrap.
- Annotations: each is recorded once as a `DrawAnnotationCommand` (core library) holding one appearance recording per state in `PdfAnnotation.SupportedStates`; the execution context's `PdfAnnotationStates` (annotation → `PdfAnnotationState`: visual state, matrix, `IsHidden`; missing = `Default`) picks the appearance at replay, so hover, press, zoom, rotation and hide actions never re-record (only a missing recording does). `PdfPanelAnnotations.CreateStates` builds them from its active annotation and pointer state (NoZoom/NoRotate matrix, visibility) for the visible and look-ahead pages into `ContentUpdateRequest.AnnotationStates`, which is how they reach the workers. Every annotation is rasterized into the tiles inside the page group; there is no live annotation replay on the frame. A state change re-rasterizes only the tiles overlapping the annotation's `ContentBounds` under the old and the new matrix, decided by the tiler from each tile's recorded states; the context sends a request with new annotation states at once. Bubbles are not part of either and are a panel feature only: which annotations get one, their size, colors and drawing are `PdfAnnotationExtensions` (`ShouldDisplayBubble`, `GetBubbleRectangle`, `RenderBubble`, over the core library's public `PdfAnnotationGraphics.GetAnnotationBubbleIcon`/`RenderIcon`); they are the top `PdfPanelGraphics` layer (`Bubbles`), recorded per page by `PdfPanelAnnotations.UpdateBubbles` from `PdfPanelContext.Render`. NoView/ToggleNoView flips only while the pointer is over the annotation's content (`PdfAnnotationPopup.IsOverContent`), never over its bubble. Words and content items come from a `PdfSnapSurface` of zero size: it draws nothing (its draw methods return at once, a group only replays its recording) and only builds the execution context's content tree. The content work item follows the cached content recording on one after the tiles and before notifying, when the words or content items are missing; the text work item renders the page into one with paths, images and shadings off; `PdfImage.DecodePixels`/`DecodePng` return null for now (stencil masks are always drawn as images).
- `PdfPanelDocument` (root namespace, created by `PdfPanelDocument.FromDocument`) is the panel's document: the `IPdfDocument` and its `DocumentLocker`, the `Pages`, the read accessors and freshness checks, the optional content states the extracted content was produced with, and the notifications (`PageUpdated`, `PageTextExtracted`, `PageThumbnailRendered`, `ExtractedContentCleared`), posted once to the synchronization context given to `FromDocument`. It creates and disposes the `PdfPanelScheduler`.
- `PdfPanelPage` is one page: label, size, rotation and offset, plus the `IPdfPage` (`PdfPage`) and the `PdfPageCacheEntry` holding everything produced for it — the same model as a PDF page owning its resources. There is no separate page-info type.
  - `Rotation` is editable (multiple of 90, normalized to 0-359) and starts from the PDF's rotation; there is no separate user rotation. `RequestPageInfo` snapshots it per request.
  - Page space is top-left, unrotated, with `UserUnit` folded in: `Size` is CropBox × UserUnit and every recording is made with `ApplyPageTransformations(CropBox, scale: UserUnit)`. Nothing else in the panel multiplies by `UserUnit`; only PDF↔page conversions read it from `PdfPage`.
  - MVVM views learn about rotation changes through `PdfThumbnailsViewModel.Synchronize()`, called next to `Layers?.Synchronize()` in WPF. Web does not report rotation: the user reads `getThumbnailLayout` (thumbnail size and rotation per page) after `setDocument` and after its own rotation or thumbnail-configuration changes.
- Thumbnails: every rendered thumbnail goes through one `PdfPageThumbnailRasterizer` owned by the scheduler, one Snap surface the size of `RenderMaximumSize`, created again when that size changes. `PdfPanelThumbnails.Request(pageNumber, formats, pin)` produces a thumbnail in `PdfPanelThumbnailFormats` (`Png`, `Samples`: opaque 8-bit RGB); a pinned one is never evicted and is re-produced in its pinned formats after a clear until `Release`. The cache entry's `Thumbnail` locker always holds one `PdfPageThumbnail` (`Png`, `Samples`), whose forms are set and cleared under its write lock; the thumbnail work item adds only the requested forms it is missing: samples encoded to PNG, an RGB PNG decoded to samples on the worker, or a rendering when it has neither. Placeholder production requests `Samples`, the view model `Png`; the renderer draws the cached `Samples` as they are (`DrawImage` of samples), with no decoding or copy of its own. `PlaceholderMode` (`None`/`VisiblePages`/`NearestPages`) is the single switch for producing thumbnails ahead and drawing them in place of undecoded pages; with `None` no thumbnail is drawn as a placeholder even when it exists. While it is not `None`, the thumbnails of visible pages are also never evicted; `MaximumCount` only evicts the rest. `ThumbnailReady` carries only the page number and fires once every requested form is ready; consumers read the cache with `GetPng` or `GetSamples` and never keep a copy. MVVM `PdfThumbnailViewModel.IsVisible` pins/releases and `Png` reads the cache while visible.
- Web thumbnails: `setThumbnailVisible(id, pageNumber, visible)` maps to `IsVisible`; the callback reports `{ pageNumber, png }` of visible thumbnails, at once when the PNG already exists. `getThumbnail` reads an already produced PNG without a callback.
- Property reads on `IPdfDocument`/`IPdfPage` are thread-safe; every PdfPixel model method that loads lazily (`GetAnnotations`, `CreatePageGroup`, `Render`, image decoding, resource resolution…) and every stream access locks `DocumentLocker`, on the UI thread as on workers: they read the shared stream. Methods that only compute from loaded properties (`GetHoverPath`, `PdfOptionalContentStates.IsVisible`) need no lock.
- `PdfPanelScheduler` (`Scheduling/`) only processes requests (content, thumbnails, text extraction) and writes results into each page's cache entry through work items on the queue; work items take the `PdfPanelPage`. Only `PdfPanelContext` and `PdfPanelThumbnails` talk to the scheduler. Content goes through one `ContentUpdateRequest` (scale, tile size, tile capacity, antialias, resolved optional content states, annotation states, and `ContentUpdatePage`s in order: page, `IsVisible`, `VisibleRegion` — the panel's part of a visible page, the whole page for a look-ahead page; pages whose larger side is more than 1.5 times the panel's larger side are not looked ahead); visible pages drive thumbnail priority and where text extraction starts, pages not in the request are cancelled and evicted. The scheduler never compares requests: the tiler compares each tile's recorded settings with the request.
- `PdfPanelContext` is created per `PdfPanelDocument`. It is the single entry point for all viewport/layout updates: it builds `PagesDrawingRequest` (plain data, no equality, the only drawing request) and decides whether the frame is drawn: `Render` submits it to `PdfPanelRenderer` when it differs from the request drawn last (`IsSameFrame`), `Present` (MVVM `RequestPresent`, and `RequestRedraw` after re-reading the host's properties) always, drawing from the cache without producing content again. `Render` also compares the request's optional content states with the stored ones, clearing extracted content when they differ. It also decides content updates: a request is built when settings, annotation states or pages differ from the last one built (sent or pending), and sent at once for the first request, a newly visible page or new annotation states, otherwise after `ZoomContentUpdateDelay`/`ScrollContentUpdateDelay` (its own timer); a pending request is replaced by a newer one.
- `PdfPanelRenderer` renders immediately from `PdfPanelDocument`; it does not talk to the scheduler. When a page is ready, `PdfPanelDocument.PageUpdated` fires on the UI thread and re-draws only that page.
- `PdfSnapSurfaceExtensions.DrawPage` takes a required `PageDrawFlags` parameter. Full render passes `PageDrawFlags.AllContent`. Partial content updates pass `PageDrawFlags.Background | PageDrawFlags.Thumbnail | PageDrawFlags.Content` — never redraw shadows on partial updates.
