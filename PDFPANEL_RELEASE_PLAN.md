# PdfPanel first release: work plan

Temporary tracking file. Delete before release. Milestone: [0.9.1](https://github.com/zayg21-pixel/pdf-pixel/milestone/1).

Legend: `[ ]` todo, `[~]` in progress, `[x]` done. `[HIGH]` marks TODOs already flagged high priority in code.

## Bugs

- [ ] WPF: copy of selected text works poorly (see also "Stable text interaction")
- [ ] JPX with mask on top: content is cropped incorrectly by ROI (verify it still reproduces first, find a corpus file)
- [ ] Web: WASM sometimes breaks on mobile phones when zooming (reproduces at least in the demo)
- [ ] Web: scroll is jagged. `PdfPixel.PdfPanel.Web/wwwroot/canvasInterop.js:150` `[HIGH]`
- [ ] Web: `CpuSkiaRenderer` does not copy content from the existing surface on recreate. `PdfPixel.PdfPanel.Web/Rendering/CpuSkiaRenderer.cs:36` `[HIGH]`
- [ ] Web: WebGL context is never destroyed. `PdfPixel.PdfPanel.Web/Rendering/CanvasGlContext.cs:102`

## Enhancements

- [ ] Stable text interaction: selection and copy behave consistently across WPF and Web
- [ ] Pre-render and caching of pages: read a couple of pages ahead when idle, tune eviction
- [ ] Reduce memory consumption where possible
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

## Suggested order

1. Bugs and TODOs above (small, unblock everything else)
2. Overlay API
3. Text interaction stabilization
4. Text search
5. Pre-render, caching, memory
6. Demo cleanup, final WASM cleanup, delete this file
