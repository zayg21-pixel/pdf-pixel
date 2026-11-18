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
- The host composites the frame natively from retained tiles; no Snap frame surface exists. Only the workers rasterize, sharing the document's `CommandCache`. Typefaces are shared, so Snap draws text under the execution context's `ContentLocker`. Decoding and tile rasterization always through the work queue (`IWorkQueue`). Panel data may hold live model objects (`PdfImage`, annotations) — the old "plain data only across threads" rule is obsolete. The one rule kept for Web: the host's draw path never reads producer state (cache entries, lockers); tiles reach it by event and are its own from then on.
- Skia is gone from the core library's code: system fonts are substituted by `PdfFontSubstitutor` (font directories plus a fallback family, from `PdfFontSubstitutorFactory.Create(loggerFactory)` for the current operating system), which loads every face as an `SfntPdfTypeface` on first use, matches by its `Metrics`, checks characters with `GetGid`, and returns a new instance of the chosen face; Snap is the only content rasterizer (Gold and Diagnostics included). Snap lives in its own project, `PdfPixel.Snap`, depending on PdfPixel through its public API only (no `InternalsVisibleTo`); its pixel-grid rules are in the public `PdfSnapGrid`, of which only the snapping the panel uses is public; its entry point is `PdfSnapSurface` at the root, and each feature folder (`Rasterization`, `Clipping`, `Painting`, `Color`, `Transparency`, `Imaging`, `Shading`, `Pattern`, `Text`) is its own namespace. Branch `panel-retained-tiles`: the panel no longer draws a frame. `PdfPanelContext.Render` updates `VisiblePages` (`PdfPanelFramePage`: size, rotation, public `ContentToPanel` with the translation snapped to whole pixels) and the tiles; the host draws the rest. `PdfPanelTiles` (`context.Tiles`, `Rendering`) is the only tile cache: the host gives `PdfPanelContext` an `IPdfPanelTileResourceFactory` (`Create(tileSize)`), each rasterized tile holds one `IPdfPanelTileResource` (`Write(samples)`, samples valid only during the call; `Dispose`), and `PdfPanelTiles` writes, reuses and disposes them (released resources kept up to the tiles of a panel-sized region, one column and one row more). The host learns of changes through `TilesChanged` (page number only, once per page per `Update` or batch) and draws `GetTiles(framePage)`: `PdfPanelTile`s (resource, destination already snapped to whole panel pixels by `PdfSnapGrid.SnapToWholeDevicePixels` with the page's `ContentToPanel`) in drawing order, the shown scale first and the scale being completed over it. Tile pixels are opaque 8-bit 4-channel in `PdfPanelRenderer.TileChannelOrder` (`Rgba` default, WPF sets `Bgra`): the tiler rasterizes in that order and `CopyOverPaper` (order-agnostic, alpha last) writes the pool slot directly. WPF (`Drawing/WpfPanelPages`) keeps one `WpfPageVisual` per visible page: a `Shadow` visual (rounded page outline in host units with a shared `DropShadowEffect`) under a `Content` visual in page content coordinates placed by `ContentToPanel` × 1/host scale + the snapped host offset; content is the page clip, white paper, the thumbnail placeholder (when `PlaceholderMode` is not `None`) and `GetTiles` as `Pbgra32` `WriteableBitmap`s (`WpfTileResource`, from `WpfTileResourceFactory`) in one unfrozen `DrawingGroup` (freezing it would freeze the bitmaps; aliased edges, linear scaling); the transform's translation is snapped and the host offset is snapped by `SnapPosition`, so panel pixels are device pixels and neighboring tiles share edges while zooming. Scrolling only changes transforms and shadow outlines; a page's content is drawn again on `TilesChanged` or when the scale, the corner radius or the placeholder mode change. Not drawn yet (TODOs at each spot): the `PdfPanelGraphics` layers (highlights, selection, bubbles; still recorded) and the user hooks `OnPanelPageRendered`/`OnPanelRendered` (kept on `PdfPanelRenderer`/`PdfPanelInterface`, not called); `PdfPanelFrame` survives only for those hooks. Web follows the same shape on the one browser thread (no worker yet; with one, the same calls would go to the worker instead of the module): `WebTileResource`/`WebThumbnailResource` own a canvas id in the JS canvas store (`globalThis.pdfPixelCanvases`, written straight from the WASM heap by `dotnet_canvas_create`/`dotnet_canvas_write_rgba`/`dotnet_canvas_delete` in `emscripten.c`; thumbnails are widened from RGB to RGBA first); after each `RequestRedraw` render, and on `scheduleFrame` (coalesced to the next animation frame, raised by `TilesChanged`, `ThumbnailReady` and `Cleared`, read back through `GetFrame`), C# sends the frame JSON (`FrameDtoConverter`: per visible page its `contentToPanel` and `panelToContent`, snapped bounds, placeholder thumbnail canvas id and tiles as canvas id plus snapped destination; the page corner radius) and the JS view draws it on its canvas: per page a white rounded rectangle casting the canvas's own `shadowBlur` shadow, clipped, then the placeholder and the tiles through `setTransform` and `drawImage`; the container's CSS background shows around the pages. `onFramePresented` still gets the overlay canvas and the frame. Thumbnails reach JS as their store canvas (`onThumbnailUpdated({ pageNumber, image })`, `getThumbnail`); the demo copies it into its own canvas. MAUI is broken until it catches up. PanelDiagnostics is the tile resource factory itself (`PanelTileLog`, pixel-less `PanelTileResource`) and logs each `TilesChanged` with tiles written and resources alive. The panel's update work item records page content and annotations into the cache entry's command recordings, then rasterizes the requested grid points with `PdfPageContentTiler` (owned by the scheduler, one Snap surface, created again with a new `PdfPageTilePool` when the request's tile size or channel order changes) and posts the page's finished tiles as one batch (`PdfPageRasterizedTile`s: page, scale, grid point, the settings each was rasterized with, pool samples returned on `Dispose`; also the part done when cancelled) through `PdfPanelDocument.TilesRasterized` to the UI thread, where `PdfPanelTiles` writes them into their resources and raises one `TilesChanged` for the page. The tiler reads the recordings from the page's cache entry itself; the cache entry holds no tiles. Each tile is drawn on the tiler's own Snap surface over transparent premultiplied 8-bit samples (cleared per tile, DeviceRGB): the page group (`IPdfPage.CreatePageGroup`, always isolated, `/Group` color space and knockout, otherwise the surface's color space) around the content recording and the annotation recording over it, drawn as its recording alone when the group declares DeviceRGB or no color space and no knockout, since the transparent root then is that group (`PdfPageRasterExtensions.GetRootContent`); the tile is then copied into its slot over white paper (`CopyOverPaper`, c + 255 − α, no float step). Thumbnails are drawn and copied the same way (RGBA surface into RGB samples). Every tile is a square `TileSize × TileSize` slot from the `PdfPageTilePool`, the whole tile surface exported into it; edge tiles reach past the page and are cut by the page clip when drawn. The pool holds only tiles in flight to the UI thread: its capacity is the number of grid points of the last request; `Rent` allocates when it has no free slot, `Return` keeps a slot while rented and free slots together stay within the capacity. The tile cache lives on the UI thread in `PdfPanelTiles`: per page a `PdfPageTileSets` (`Current`, the set shown, and `Next`, a set of a new scale or tile size being completed), each a `PdfPageTiles` over its grid (`Initialize(grid)` returns the rasterized tiles it dropped, `FindTile`, `IsComplete`, `GetRasterizedTiles`); each `PdfPageTile` holds its resource and the antialias, optional content states and annotation states it was rasterized with, all `null` while empty. `PdfPanelTiles.Update` (from `Render`) brings each content page's set to the grid of its region (`VisibleRegion` for a visible page, the whole page ahead), releases dropped tiles and the tiles of pages that left, and returns the request pages with the grid points still needed: empty tiles, and tiles whose antialias or optional content states differ or which overlap an annotation whose state differs (`PdfAnnotationExtensions.GetChangedAnnotationRegions`). A tile arriving for a point its set no longer holds is dropped; otherwise it is written into its resource (rented when empty). When `Next` is complete it becomes `Current` and the old set's tiles are released, so the old scale stays shown (scaled while zooming) until then. Every new request cancels the pages' in-flight tiles, so each request asks again for all points still needed. Eviction of pages that left the request is queued before their replacements' updates. No annotation picture exists anymore.
- Images: a `PdfImage` resolves its `/ColorSpace` at load (null for an image mask and for a JPX image that leaves it to its data; inline images resolve a name in the current resources, and the resolved space is part of `InlineImageKey`) and owns one decoder (`Decoder`). `DrawImageCommand` holds the image, the paint (the fill paint; for a non-mask image with the current resources' device mapping and the image's `/Intent`) and the group source; Snap takes the mask decoder from `SoftMask`/`Mask` by `MaskKind` and the sample color space from the decode info. Decoders get an `IPdfImageCache` (tile size; `Rent`, `Put`, `Find`, `Release` per image) in `SelectTiles`, which decodes whole images itself, and `ReleaseTiles` after each draw returns everything to it. Snap's is `PdfSnapImageCache` on `PdfSnapSurfaceParameters`: put data is kept within its put capacity (counted by the arrays it holds alive), least recently released images dropped first, and rented memory reused within its rent capacity; the panel's scheduler uses 32 MB / 32 MB, the frame renderer none.
- Page group: `IPdfPage.Render` emits the content commands only; `CreatePageGroup(DrawRecordingCommand)` wraps any recording (content plus annotations) in the page group. Gold, Diagnostics, Demo.Cli, the Examples, the tiler and the thumbnail rasterizer wrap; Gold, the tiler and the thumbnail rasterizer then draw only the recording when the group declares DeviceRGB or no color space and no knockout, as their transparent DeviceRGB root is that group; a caller that does not want page-group blending can skip it. Content and word extraction do not wrap.
- Annotations: each is recorded once as a `DrawAnnotationCommand` (core library) holding one appearance recording per state in `PdfAnnotation.SupportedStates`; the execution context's `PdfAnnotationStates` (annotation → `PdfAnnotationState`: visual state, matrix, `IsHidden`; missing = `Default`) picks the appearance at replay, so hover, press, zoom, rotation and hide actions never re-record (only a missing recording does). `PdfPanelAnnotations.CreateStates` builds them from its active annotation and pointer state (NoZoom/NoRotate matrix, visibility) for the visible and look-ahead pages into `ContentUpdateRequest.AnnotationStates`, which is how they reach the workers. Every annotation is rasterized into the tiles inside the page group; there is no live annotation replay on the frame. A state change re-rasterizes only the tiles overlapping the annotation's `ContentBounds` under the old and the new matrix, decided by `PdfPanelTiles` from each tile's recorded states; the context sends a request with new annotation states at once. Bubbles are not part of either and are a panel feature only: which annotations get one, their size, colors and drawing are `PdfAnnotationExtensions` (`ShouldDisplayBubble`, `GetBubbleRectangle`, `RenderBubble`, over the core library's public `PdfAnnotationGraphics.GetAnnotationBubbleIcon`/`RenderIcon`); they are the top `PdfPanelGraphics` layer (`Bubbles`), recorded per page by `PdfPanelAnnotations.UpdateBubbles` from `PdfPanelContext.Render`. NoView/ToggleNoView flips only while the pointer is over the annotation's content (`PdfAnnotationPopup.IsOverContent`), never over its bubble. Words and content items come from a `PdfSnapSurface` of zero size: it draws nothing (its draw methods return at once, a group only replays its recording) and only builds the execution context's content tree. The content work item follows the cached content recording on one after the tiles and before notifying, when the words or content items are missing; the text work item renders the page into one with paths, images and shadings off; `PdfImage.DecodePixels`/`DecodePng` return null for now (stencil masks are always drawn as images).
- `PdfPanelDocument` (root namespace, created by `PdfPanelDocument.FromDocument`) is the panel's document: the `IPdfDocument` and its `DocumentLocker`, the `Pages`, the read accessors and freshness checks, the optional content states the extracted content was produced with, and the notifications (`TilesRasterized`, whose handler owns the tiles, `PageTextExtracted`, `ThumbnailRendered`, `ExtractedContentCleared`), posted once to the synchronization context given to `FromDocument`. It creates and disposes the `PdfPanelScheduler`.
- `PdfPanelPage` is one page: label, size, rotation and offset, plus the `IPdfPage` (`PdfPage`) and the `PdfPageCacheEntry` holding everything produced for it — the same model as a PDF page owning its resources. There is no separate page-info type.
  - `Rotation` is editable (multiple of 90, normalized to 0-359) and starts from the PDF's rotation; there is no separate user rotation. `RequestPageInfo` snapshots it per request.
  - Page space is top-left, unrotated, with `UserUnit` folded in: `Size` is CropBox × UserUnit and every recording is made with `ApplyPageTransformations(CropBox, scale: UserUnit)`. Nothing else in the panel multiplies by `UserUnit`; only PDF↔page conversions read it from `PdfPage`.
  - MVVM views learn about rotation changes through `PdfThumbnailsViewModel.Synchronize()`, called next to `Layers?.Synchronize()` in WPF. Web does not report rotation: the user reads `getThumbnailLayout` (thumbnail size and rotation per page) after `setDocument` and after its own rotation or thumbnail-configuration changes.
- Thumbnails: `PdfPanelThumbnails` (UI thread) is the only thumbnail cache: the host gives `PdfPanelContext` an `IPdfPanelThumbnailResourceFactory` (`Create(samples)`, opaque 8-bit RGB samples given as they are, valid only during the call; no formats, no PNG), and each produced thumbnail is kept as its `IPdfPanelThumbnailResource` (disposed on eviction or clear) in a `PdfPageThumbnailEntry` (requested, pending, resource). The API is plain: `Request(page)` (ignored when the page is already requested, pending or produced), `Cancel(page)` (stops a pending one; a produced one becomes evictable), `Get(page)`, `ThumbnailReady` (page number) and `Cleared` (once, when `UsePageThumbnails`/`RenderMaximumSize` or the layers' optional content states changed; requested ones are produced again). Nothing is counted: the host is the single requester. `MaximumCount` evicts cancelled thumbnails in the order they were cancelled (a list, no stamps). The scheduler only keeps the requested pages with their observers: `RequestThumbnail(page)` queues one, `CancelThumbnail(page)` cancels it, and each content request cancels and queues the pending ones again behind the tiles. The thumbnail work item hands every uncancelled result straight to `PdfPanelDocument.NotifyThumbnailRendered`, the way the content work item hands over tiles (no completion object, no `BackgroundWorkItemResult`, which only text extraction uses to continue its chain); it is posted as `PdfPageRenderedThumbnail` (page, its request's observer, samples or `null` when it failed) through `PdfPanelDocument.ThumbnailRendered`; on the UI thread `PdfPanelThumbnails` asks `CompleteThumbnail`, which accepts it only while that observer is still the page's request and forgets the request, so a result posted before a cancel or a clear is dropped; settings come as `PdfPanelThumbnailSchedulingSettings` (use page thumbnails, render maximum size). Every rendered thumbnail goes through one `PdfPageThumbnailRasterizer` owned by the scheduler, one Snap surface the size of `RenderMaximumSize`, created again when that size changes; the work item decodes an RGB PNG of the page's thumbnail image to samples or renders the page. MVVM `PdfThumbnailViewModel.IsVisible` only records visibility (the owner raises `PdfThumbnailsViewModel.VisibleItemsChanged`) and `Resource` reads `Get` while visible (the demo binds `Resource.Bitmap`). `PlaceholderMode` lives on `PdfPanelConfiguration.ThumbnailPlaceholderMode` (default `VisiblePages`), read by the host and not applied to the context. The core's `PdfPanelThumbnailRequests` (wanted pages in, `Request`/`Cancel` for the difference out) is each host's single requester; in WPF: after each render and on `VisibleItemsChanged`, `WpfPdfPanel` computes the wanted pages (the sidebar's visible items, plus the visible pages while placeholders are on; modes `None` and `VisiblePages` only) and requests the new ones and cancels the dropped ones; `WpfPanelPages` draws `Get(page)` (`WpfPdfPanelThumbnailResource`, a frozen `Rgb24` `BitmapSource`) under the tiles.
- Web thumbnails: `setThumbnailVisible(id, pageNumber, visible)` maps to `IsVisible`; the callback reports `{ pageNumber, image }` (the store canvas) of visible thumbnails, at once when it is already produced. `getThumbnail` returns an already produced canvas without a callback. The Web host requests through the core's `PdfPanelThumbnailRequests` like WPF: the visible items plus, while placeholders are on, the visible pages.
- Property reads on `IPdfDocument`/`IPdfPage` are thread-safe; every PdfPixel model method that loads lazily (`GetAnnotations`, `CreatePageGroup`, `Render`, image decoding, resource resolution…) and every stream access locks `DocumentLocker`, on the UI thread as on workers: they read the shared stream. Methods that only compute from loaded properties (`GetHoverPath`, `PdfOptionalContentStates.IsVisible`) need no lock.
- `PdfPanelScheduler` (`Scheduling/`) only processes requests (content, thumbnails, text extraction) through work items on the queue, writing recordings and words into each page's cache entry and posting tiles and thumbnails to the UI thread; work items take the `PdfPanelPage`. Only `PdfPanelContext` and `PdfPanelThumbnails` talk to the scheduler. Content goes through one `ContentUpdateRequest` (scale, tile size, tile channel order, antialias, resolved optional content states, annotation states, and `ContentUpdatePage`s in order: page, `IsVisible`, `GridPoints` — the tiles to rasterize, computed by `PdfPanelTiles`; pages whose larger side is more than 1.5 times the panel's larger side are not looked ahead); visible pages drive where text extraction starts, pages not in the request are cancelled and evicted. The scheduler never compares requests and rasterizes exactly the points asked for.
- `PdfPanelContext` is created per `PdfPanelDocument` (no render target). It is the single entry point for all viewport/layout updates. `Render` updates `VisiblePages` and the tiles, and compares the layers' optional content states with the stored ones, clearing extracted content when they differ; MVVM `RequestPresent`/`RequestRedraw` call it again (TODO: redraw the overlays). It also decides content updates: a request is sent when the settings or annotation states differ from the last one built (sent or pending), a page appeared, left or changed visibility, or a page needs a grid point the last request did not ask for; at once for the first request, a newly visible page or new annotation states, otherwise after `ZoomContentUpdateDelay`/`ScrollContentUpdateDelay` (its own timer); a pending request is replaced by a newer one.
- `PdfPanelRenderer` holds only settings (background, corner radius, antialias, tile size and channel order, delays, look-ahead, the unhooked user graphics hooks); it draws nothing and does not talk to the scheduler.
