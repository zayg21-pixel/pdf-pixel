# PdfPanel first release: work plan

Temporary tracking file. Delete before release. Milestone: [0.9.1](https://github.com/zayg21-pixel/pdf-pixel/milestone/1).

Legend: `[ ]` todo, `[~]` in progress, `[x]` done. `[HIGH]` marks TODOs already flagged high priority in code.

## Bugs

- [ ] WPF: copy of selected text works poorly (see also "Stable text interaction")
- [ ] Web: WASM sometimes breaks on mobile phones when zooming (reproduces at least in the demo)
- [ ] Web: scroll is jagged. `PdfPixel.PdfPanel.Web/wwwroot/canvasInterop.js:150` `[HIGH]`
- [ ] Web: `CpuSkiaRenderer` does not copy content from the existing surface on recreate. `PdfPixel.PdfPanel.Web/Rendering/CpuSkiaRenderer.cs:36` `[HIGH]`
- [ ] Web: WebGL context is never destroyed. `PdfPixel.PdfPanel.Web/Rendering/CanvasGlContext.cs:102`

## Enhancements

- [ ] Stable text interaction: selection and copy behave consistently across WPF and Web
- [ ] Pre-render and caching of pages: read a couple of pages ahead when idle, tune eviction
- [ ] Reduce memory consumption where possible
- [ ] Split the content update delay into separate scroll and zoom delays. Scroll defaults to 0 (decode right away), zoom keeps the current 200 ms. `PdfPixel.PdfPanel/Rendering/PdfPanelRendererProperties.cs:48` (`ContentUpdateDelay`), `PdfPanelRenderer.cs:48`, `:104`
- [ ] Web: parse type in interop or refactor the JS to remove the need. `PdfPixel.PdfPanel.Web/PdfPanelInterop.cs:285` `[HIGH]`
- [ ] Remote file loading: implement or drop the TODO. `WpfPdfPanel.cs:359`, `PdfPanelInterop.cs:356`
- [ ] Cleanup demo files (`PdfPixel.Demo.Wpf`, `PdfPixel.Demo.Web`)
- [ ] Cleanup WASM demo: JS files contain leftovers from multi-threading support

## New implementation

### Finalized API for rendering on top of pages

Same path for WPF/MAUI and other native hosts, a separate one for WASM. Search and selection highlights do not depend on it; they are drawn by `PdfPanelTextLayer`.

- [ ] Define the overlay contract (what the host draws, in which coordinate space, draw order relative to page content)
- [ ] Native implementation (WPF first, shape it so MAUI can reuse it)
- [ ] WASM implementation
- [ ] Use it from the demos

### Text search

Biggest item. Highlights are drawn by `PdfPanelTextLayer` itself, so search no longer waits for the overlay API.

- [x] Per-page text extraction without rendering: `PdfTextExtractionCommandProcessor` (no Skia, replays nested form recordings) run by `PdfPageExtractTextWorkItem`. Characters are kept per page in `PdfPageCacheEntryItem.Characters` (`null` = not extracted) and survive page-out. `PdfPanelContext.ExtractText` keeps exactly one text item queued, so rendering waits for at most one page. `PageTextExtracted` is raised by both text extraction and rendering
- [~] Search engine: `PdfPanelTextSearchEngine` over `PdfPanelTextLayer` text, incremental (`MatchesChanged` on every page with matches). Not cancellable by decision. No progress reporting yet
- [~] Matching rules: `MatchCase` and `WholeWord` done. Diacritics, ligatures, hyphenation and inferred spaces between words (characters are joined as extracted) not done
- [x] Match model: `PdfPanelSearchMatch` = `PdfPanelTextRange` (page, start index, length) + bounds. Selection uses the same range type
- [~] Highlight rendering: all matches of the visible pages drawn in `SearchMatchColor` under the selection. No separate highlight for the current match
- [~] Navigation: `ScrollToSearchMatch` context extension, count via the results. No next/previous in the API (host side)
- [x] Public API: `PdfPanelContext.SearchQuery`, `SearchOptions`, `ExtractText`; results on `PdfPanelRenderer.TextSearchEngine`; `PdfPanelRenderer` orchestrates extraction → search → page redraw
- [~] UI: WPF done (`SearchQuery`, `SearchResults`, `CurrentSearchResult` on `WpfPdfPanel`; demo search box with results drop-down, navigates on hover). Web panel and Web demo not done
- [ ] Tests
- [ ] Text inside soft-mask forms is extracted like page text, in both rendering and text extraction

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

## Suggested order

1. Missing functionality first, starting with text search. Done for the core and WPF; Web and the open items above remain
2. Overlay API
3. Refactor where needed: text selection moves onto the same text layer as search. Done (`PdfPanelTextLayer`)
4. Bugs and TODOs above
5. Pre-render, caching, memory
6. Demo cleanup, final WASM cleanup, delete this file
