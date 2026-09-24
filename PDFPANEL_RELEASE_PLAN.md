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

Same path for WPF/MAUI and other native hosts, a separate one for WASM. Needed before search highlights can be drawn.

- [ ] Define the overlay contract (what the host draws, in which coordinate space, draw order relative to page content)
- [ ] Native implementation (WPF first, shape it so MAUI can reuse it)
- [ ] WASM implementation
- [ ] Use it from the demos

### Text search

Biggest item. Depends on the overlay API for match highlighting.

- [ ] Per-page text extraction with positions, reusing what text selection already produces
- [ ] Search engine: background, cancellable, incremental across pages, progress reporting
- [ ] Matching rules: case, diacritics, ligatures, whitespace and line-break handling, hyphenation
- [ ] Match model: page, range, bounding rectangles per match
- [ ] Highlight rendering of all matches and the current one (via overlay API)
- [ ] Navigation: next/previous match, scroll to match, match count
- [ ] Public API on `PdfPanelContext`
- [ ] UI in WPF demo and Web demo
- [ ] Tests

## Investigations

- [x] Aggressive optimizations: `AggressiveOptimization` on the per-pixel and per-sample loops of Imaging.Processing, PdfPixel.Color, JPX, JBIG2, CCITT, JPEG, shadings and functions (`#if !NETSTANDARD2_0`, the flag does not exist there). Measured in Release with `PdfPixel.Diagnostics`, first iteration vs. steady state:
  - bug1749563 (JPX), page 1, scale 4: first iteration 1222 → ~815 ms, steady ~550 → ~575–610 ms
  - Wheeling.VA.Daily.Intelligencer (JBIG2), page 1: first-iteration decode 6811 → 6006 ms, steady unchanged
  - 3i_2021 (CMYK ICC), page 1, scale 4: first iteration 649 → 582 ms, steady unchanged
  - Tagged code gets no Dynamic PGO. Accepted: documents are decoded once and cached, the cold run is what the user waits for. Loop helpers called from tagged methods need `AggressiveInlining`, a tagged caller does not inline loops on its own
  - PdfPixel.PostScript left untagged

## Suggested order

1. Bugs and TODOs above (small, unblock everything else)
2. Overlay API
3. Text interaction stabilization
4. Text search
5. Pre-render, caching, memory
6. Demo cleanup, final WASM cleanup, delete this file
