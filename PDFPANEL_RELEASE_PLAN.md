# PdfPanel first release: work plan

Temporary tracking file. Delete before release. Milestone: [0.9.1](https://github.com/zayg21-pixel/pdf-pixel/milestone/1).

Legend: `[ ]` todo, `[~]` in progress, `[x]` done. `[HIGH]` marks TODOs already flagged high priority in code.

Stages are worked in order. A later stage may start early only when it does not touch what an earlier one is changing.

## Stage 0: Implementation blockers

None known. Core text search, text extraction and the shared `PdfPanelTextLayer` are in place; everything below is cleanup, fixes, API shape or new functionality on top of a working panel.

## Stage 1: Small cleanups

- [ ] Cleanup WASM panel JS (`PdfPixel.PdfPanel.Web/wwwroot/canvasInterop.js` and related): leftovers from multi-threading support. Includes the `[HIGH]` annotation popup TODO (`PdfPanelInterop.cs`, `CreateAnnotationPopupState`): the JS popup `type` is always empty and never read (removed in `41195e99`); drop it so the popup matches `PdfAnnotationPopup` (`isInteractive`, `messages`). Keep the reply hierarchy: today `AddAnnotationPopupMessages` flattens `Replies` depth-first into one list, while WPF shows them nested. Build the tree with JSImport factories (`createAnnotationMessage(title, content, date)` returning a `JSObject`, `addAnnotationReply(parent, reply)`, `setAnnotationPopup(state, isInteractive, messages)`) so JS gets `{ isInteractive, messages: [{ title, content, date, replies }] }`, and rebuild it only when the active popup changes, not on every redraw. Also rename the JS state keys `viewportWidth`/`viewportHeight` to the panel naming

## Stage 2: Easy wins and fixes

- [x] `PdfYieldingExecutionObserver`: takes a yield interval and only yields once that much time has passed since the last yield. Web reads it from the `yieldInterval` configuration key (ms, default 16)
- [x] Split the content update delay into `PdfPanelRenderingSettings.ScrollContentUpdateDelay` (0) and `ZoomContentUpdateDelay` (200 ms)
- [ ] Web: `CpuSkiaRenderer` does not copy content from the existing surface on recreate. `PdfPixel.PdfPanel.Web/Rendering/CpuSkiaRenderer.cs:36` `[HIGH]`
- [ ] Web: WebGL context is never destroyed. `PdfPixel.PdfPanel.Web/Rendering/CanvasGlContext.cs:102`

## Stage 3: API unification

Biggest item. Goal: one way to configure and drive the panel across WPF and Web, without mirroring every `PdfPanelContext` property as a WPF dependency property.

State before the settings refactor:
- WPF mirrors context state as dependency properties (`Scale`, `MinScale`, `MaxScale`, `PageGap`, `PagesPadding`, `CurrentPage`, `AutoScaleMode`, `SearchQuery`, `SearchResults`, `CurrentSearchResult`, `PageLabel`, `AnnotationPopup`) and copies them into the context on every `SyncViewerCanvasState`. Host-only knobs sit next to them (`ScrollTick`, `ScaleFactor`)
- `BackgroundColor`, `PageCornerRadius` are plain properties that only reach `PdfPanelRendererProperties` when the renderer is created; changing them later has no effect
- `WpfPdfPanelInterface` is a request relay (`ZoomIn`, `ZoomOut`, `RequestRedraw`, `RequestRefresh`) plus the only drawing hook (`OnAfterDraw`)
- Web configures through `PdfPanelConfiguration` with different names (`MinZoom`/`MaxZoom` vs `MinScale`/`MaxScale`, `MinimumPageGap` vs `PageGap`) and a JS state object per `RequestRedraw`
- Context and renderer are recreated whenever `Pages` changes (`EnsureViewerCanvas`) and do not exist before `Loaded`

Also:
- `PdfRenderingParameters` / `PdfCommandExecutionParameters` (core library internals) are exposed on the context, and `PdfRenderingParameters` is shared by reference with the decode thread
- Three tile sizes: `PdfRenderingParameters.ImageTileSize` (256), `PdfCommandExecutionParameters.ImageTileSize` (1024), `PdfPanelRendererProperties.TileSize` (512)
- `ExtractText` means two things on the context: `context.ExtractText` (extract all pages for search) vs `context.RenderingParameters.ExtractText` (collect text while rendering)
- Defaults and names differ per layer: `MaxScale` 10 (context) vs 50 (WPF), gap/padding 10 vs 20, `PageGap` / `MinimumPageGap` / `MinZoom`
- Page gap and padding are context properties passed into `IPdfPanelLayout` methods instead of belonging to the layout
- `AutoScaleMode` and the zoom step are not stored in the context

Agreed design:
- Three kinds of input:
  - **Host wiring**: constructor arguments only, never settable (`SynchronizationContext`, surface factory, render target factory, work queue, observer factory). `SynchronizationContext` leaves the renderer properties
  - **State**: changes through interaction, lives on `PdfPanelContext` (`Scale`, offsets, viewport size, pointer, `AutoScaleMode`, `SearchQuery`)
  - **Settings**: a single `PdfPanelSettings` instance
- `PdfPanelSettings` has get-set groups by user concern: `Appearance`, `Zoom`, `Layout` (`IPdfPanelLayout` itself, owning `PageGap` and `Padding`), `Interaction`, `Search`, `Rendering`. Groups are mutable classes with get-set properties (no constructor parameters per setting)
- One settings instance is passed to the constructors of both `PdfPanelContext` and `PdfPanelRenderer`. No change notification: `Synchronize` propagates every setting (rebuilding what is baked at creation, e.g. tiler `TileSize`, clock `AnimationFps`, when the value differs from the last applied one), and the following `Render` always uses the new values
- Drawing never reads settings directly: `Render` copies the `Appearance` and `Rendering` groups into the request. Partial redraws (page decoded, text extracted, animation tick) draw from `_lastRequest`, so an unsynchronized change never shows up on a single page. The decode thread gets the same copy
- WPF: changing a group member (`Appearance.BackgroundColor = x`) does not trigger a redraw by itself; the host requests one. Replacing a group through its dependency property does
- `PdfRenderingParameters` / `PdfCommandExecutionParameters` are not panel API. `PdfPageUpdateCacheWorkItem` builds them from the request's `Rendering` copy (`Antialias`, `SnapToDevicePixels`; `CacheDecodedTiles` always on). The core image tile sizes keep their library defaults; only the panel tile size is a setting
- WPF: the panel owns the settings instance and it outlives context recreation on document switch. Individual groups are bindable dependency properties with a `null` default; the panel constructor assigns its own instances (a mutable default would be shared by every panel). Dependency properties otherwise only for state and `Pages`
- Web: `PdfPanelSettings` is built from the configuration passed on canvas registration and is not updated afterwards

Open:
- Scroll step: WPF `ScrollTick` (100, per wheel notch) and JS `scrollStep` (20, per wheel line) are still host-side and differ in meaning

Work:
- [x] Full settings inventory mapped to groups, with one name and one default per setting
- [x] `PdfPanelSettings` and groups; context and renderer take it in the constructor
- [x] Core parameters built internally; remove `RenderingParameters` / `CommandExecutionParameters` from the context and the request
- [x] Move `AutoScaleMode` into context state (applied by `Synchronize`) and the zoom step into `Zoom.ZoomStep`
- [ ] Replace `WpfPdfPanelInterface` with calls on the context (zoom/redraw/refresh); the drawing hook moves to the overlay contract below
- [ ] Expose the context to hosts
- [x] WPF: group dependency properties, drop mirrored settings dependency properties
- [x] Web: build settings on registration. The JS configuration's `settings` object mirrors `PdfPanelSettings` in camelCase (`settings.zoom.minScale`, `settings.layout.padding.left`, ...); host-only keys (`useWebGL`, `scrollStep`, `yieldInterval`) sit next to it
- [x] Update both demos to the new API

## Stage 4: New functionality

### Finalized API for rendering on top of pages

Search and selection highlights do not depend on it; they are drawn by `PdfPanelTextLayer`. `PdfPanelInputProcessor` stays internal: it is not the user interaction API.

Agreed design:
- Interaction uses the host's own events (WPF `MouseMove`/`MouseDown`/`MouseUp`, DOM pointer events). The user updates their own state and requests a redraw (`RequestRefresh` for overlay only, `RequestRedraw` for everything)
- A redraw ends in a present, and the present invokes the user callback with a frame object describing what was presented:
  - Native hosts (WPF, later MAUI): `OnAfterDraw(SKCanvas, frame)`; the canvas is in panel pixels
  - WASM (this release, after the JS refactor): C# builds the same `PdfPanelFrame` at present and passes it to a JS interop function. The public JS API adds the panel's HTML canvas element and the panel id and raises it to the user's subscribers, so JS code can subscribe to panel changes. Drawing (e.g. on an own canvas on top) and routing pointer events stay on the user's side
- Frame object `PdfPanelFrame`, pages `PdfPanelFramePage`, all conversions are `PdfMatrix`:
  - `HostToPanel`: host coordinates to panel pixels, provided by the render target (`IPdfPanelRenderTarget.HostToPanel`). WPF: DIPs relative to the panel, DPI scale plus snapped offset. Web: identity for now (JS already sends panel pixels)
  - `PanelSize`
  - `Pages` (`PdfPanelFramePage`): the public part of `VisiblePageInfo`
    - `PageNumber`, `Info` (`PdfPanelPageInfo`: `Label`, `CropBox` in PDF user space, `Rotation`), `UserRotation`, `RotatedSize`
    - `GetPanelToPage(rotation)`: panel pixels to page space rotated by `rotation` (multiple of 90), top-left origin. `0` = content as authored (text bounds), `Info.Rotation` = as the document intends, `Info.Rotation + UserRotation` = as displayed
    - Not exposed: `Offset` (folded into the matrix), `RegionOfInterest`, `FromPdfRect`. PDF user space is reached through `Info.CropBox` (Y flip + crop origin)
  - Chain: host point → `PdfPoint` → `HostToPanel` → panel point → `GetPanelToPage(rotation)` → page point. Drawing on a page: concat the inverse onto the canvas. `HostToPanel` has no rotation: host and panel are both screen-aligned, top-left origin
- The frame is built at present from the presented request and the sizes the render target already tracks. Nothing about the last frame is stored on the panel
- `IPdfPanelRenderTarget.Render` takes the frame instead of `DrawingRequest`; `DrawingRequest` and `VisiblePageInfo` become internal
- Naming: "Host" for host coordinates, "Panel" for panel pixel space. "Canvas" and "Viewport" as names of the panel space go away (`CanvasSize`/`CanvasScale`/`CanvasOffset`/`GetCanvasPosition`, `ViewportWidth`/`ViewportHeight`, `ViewportPosition`, `ViewportToPageMatrix`, `GetContentToCanvasMatrix`, `RegisterCanvas`). Real canvases keep the name (`SKCanvas`, the HTML canvas element, `CanvasSelector`)

Work:
- [x] `PdfPanelFrame` / `PdfPanelFramePage` and `IPdfPanelRenderTarget.Render(SKSurface, PdfPanelFrame)` with `IPdfPanelRenderTarget.HostToPanel`
- [x] Decode pipeline internal: `IPdfPageContentProvider` removed; `PdfPageContentProvider` keeps only its constructor and `Dispose` public; requests, `VisiblePageInfo`, cache entries, work items, content pictures and the tiler are internal; `PdfPanelRenderer.Submit`/`ContentProvider` and the text layer / search engine constructors are internal
- [x] Canvas/Viewport → Host/Panel renames (WPF `IScrollInfo.ViewportWidth`/`ViewportHeight` stay: interface members)
- [x] WPF: remove `CanvasMouseEventArgs` and `CanvasMouseDown`/`Up`/`Move`; `OnAfterDraw` receives the frame; `CanvasSize`/`CanvasScale`/`CanvasOffset`/`GetCanvasPosition` no longer public
- [x] WPF demo uses the frame
- [ ] WASM: C# builds `PdfPanelFrame` at present and calls a JS interop function; the public JS API adds the HTML canvas and panel id and raises it to subscribers. After the JS refactor (stage 1)

### Text search

Highlights are drawn by `PdfPanelTextLayer` itself, so search no longer waits for the overlay API.

- [x] Per-page text extraction without rendering: `PdfTextExtractionCommandProcessor` (no Skia, replays nested form recordings) run by `PdfPageExtractTextWorkItem`. Characters are kept per page in `PdfPageCacheEntryItem.Characters` (`null` = not extracted) and survive page-out. `PdfPanelContext.ExtractText` keeps exactly one text item queued, so rendering waits for at most one page. `PageTextExtracted` is raised by both text extraction and rendering
- [~] Search engine: `PdfPanelTextSearchEngine` over `PdfPanelTextLayer` text, incremental (`MatchesChanged` on every page with matches). Not cancellable by decision. No progress reporting yet
- [~] Matching rules: `MatchCase` and `WholeWord` done. Diacritics, ligatures, hyphenation and inferred spaces between words (characters are joined as extracted) not done
- [x] Match model: `PdfPanelSearchMatch` = `PdfPanelTextRange` (page, start index, length) + bounds. Selection uses the same range type
- [~] Highlight rendering: all matches of the visible pages drawn in `SearchMatchColor` under the selection. No separate highlight for the current match
- [~] Navigation: `ScrollToSearchMatch` context extension, count via the results. No next/previous in the API (host side)
- [x] Public API: `PdfPanelContext.SearchQuery`, `PdfPanelSettings.Search`, `ExtractText`; results on `PdfPanelRenderer.TextSearchEngine`; `PdfPanelRenderer` orchestrates extraction → search → page redraw
- [~] UI: WPF done (`SearchQuery`, `SearchResults`, `CurrentSearchResult` on `WpfPdfPanel`; demo search box with results drop-down, navigates on hover). Web panel and Web demo not done. Web side should be built on the stage 3 API
- [ ] Tests
- [ ] Text inside soft-mask forms is extracted like page text, in both rendering and text extraction

### Stable text interaction

- [ ] Selection and copy behave consistently across WPF and Web
- [ ] WPF: copy of selected text works poorly

## Stage 5: Harder bugs

Need investigation before a fix is known.

- [ ] Web: scroll is jagged. `PdfPixel.PdfPanel.Web/wwwroot/canvasInterop.js:150` `[HIGH]`
- [ ] Web: WASM sometimes breaks on mobile phones when zooming (reproduces at least in the demo)

## Stage 6: Pre-render, caching, memory

- [ ] Pre-render and caching of pages: read a couple of pages ahead when idle, tune eviction
- [ ] Reduce memory consumption where possible

## Stage 7: Release cleanup

- [ ] Cleanup demo files (`PdfPixel.Demo.Wpf`, `PdfPixel.Demo.Web`)
- [ ] Final WASM cleanup
- [ ] Delete this file

## Investigations

- [x] Aggressive optimizations: `AggressiveOptimization` on the per-pixel and per-sample loops of Imaging.Processing, PdfPixel.Color, JPX, JBIG2, CCITT, JPEG, shadings and functions (`#if !NETSTANDARD2_0`, the flag does not exist there). Measured in Release with `PdfPixel.Diagnostics`, first iteration vs. steady state:
  - bug1749563 (JPX), page 1, scale 4: first iteration 1222 → ~815 ms, steady ~550 → ~575–610 ms
  - Wheeling.VA.Daily.Intelligencer (JBIG2), page 1: first-iteration decode 6811 → 6006 ms, steady unchanged
  - 3i_2021 (CMYK ICC), page 1, scale 4: first iteration 649 → 582 ms, steady unchanged
  - Tagged code gets no Dynamic PGO. Accepted: documents are decoded once and cached, the cold run is what the user waits for. Loop helpers called from tagged methods need `AggressiveInlining`, a tagged caller does not inline loops on its own
  - PdfPixel.PostScript left untagged
- [x] Text extraction performance, text-only (no paths, images, shadings), all pages, characters of every page held, Release:
  - PDF32000_2008_unlocked, 756 pages, 1.92M characters: ~2.4–2.5 s cold, ~0.75–0.9 s warm, 66.5 MB live
  - pdf.pdf.pdf, 1310 pages, 2.43M characters: ~3.1–3.3 s cold, ~1.15 s warm, 116 MB live
  - Allocations 1332 → 664 MB: parser value buffers reused, flattener buffer, per-block character buffer, no `ShapedGlyph[]` copy when nothing keeps the glyphs (span through `DrawTextSequence`), `AggressiveOptimization` on the text hot path
