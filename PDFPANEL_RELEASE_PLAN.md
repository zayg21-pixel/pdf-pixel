# PdfPanel first release: work plan

Temporary tracking file. Delete before release. Milestone: [0.9.1](https://github.com/zayg21-pixel/pdf-pixel/milestone/1).

Legend: `[ ]` todo, `[~]` in progress, `[x]` done.

Implementation is complete. What is left are bugs and cleanups. Finished work, agreed designs and measurements are in the git history of this file.

## Bugs

- [ ] Text inside soft-mask forms is extracted like page text, in both rendering and text extraction. Fix belongs in PdfPixel (core)
- [ ] Web: WASM sometimes breaks on mobile phones when zooming (reproduces at least in the demo). Needs investigation before a fix is known

## Cleanups

- [ ] Check that every `WpfPdfPanelInterface` request (zoom/redraw/refresh) goes through `Synchronize`, for consistency
- [ ] Scroll step: WPF `ScrollTick` (100, per wheel notch) and JS `scrollStep` (20, per wheel line) are host-side and differ in meaning
- [ ] Cleanup demo files (`PdfPixel.Demo.Wpf`, `PdfPixel.Demo.Web`): commented-out code and leftovers
- [ ] Final WASM cleanup

## Pre-render, caching, memory

- [ ] Pre-render and caching of pages: read a couple of pages ahead when idle, tune eviction
- [ ] Reduce memory consumption where possible

## Release

- [ ] Delete this file
