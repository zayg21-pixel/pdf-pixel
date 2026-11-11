# Memory

PdfPanel retained and peak memory while scrolling graphics-heavy documents.

Scenario: `3i_2021.pdf` in PanelDiagnostics, script `size 960 1000`, `zoom 1.4`, `sleep 2`, then 450 × (`scroll_relative 0 400`, `sleep 0.016`), `sleep 5` (~154 pages). Run `PdfPixel.PanelDiagnostics <pdf> <script> --profile memory`; `PDFPIXEL_TILE_SIZE` sets the tile size. The report gives peak private memory, live memory after full collections, what is retained at the end, and large object heap allocations by type and caller.

Now (2026-11-15): tile 1024 peaks at 450–515 MB private, ~210 MB retained, 0.9–1.1 GB on the large object heap; tile 256 peaks at 310–343 MB, ~190 MB retained. Live memory grows from ~90 MB to 190–250 MB over the scroll. Identical runs differ by up to 200 MB of peak and 300 MB on the large object heap: compare two runs each.

End-and-back (2026-11-16, tile 512, tile pool capped by the request's tile capacity, words sliced): peaks at ~450 MB with ~300 MB alive at the end, 658 MB on the large object heap. Before (2026-11-15): tile 1024 peaked at ~600 MB with ~430 MB alive. Alive at the end (dump, tile 1024): Snap pools 180 MB, ICC copies 80 MB, extracted text ~72 MB, surface roots 32 MB, PDF bytes 19 MB, tile slots 12 MB, decoded image tiles 8 MB, ~47 MB untraced.

## Open

00. Measure the image cache (`PdfSnapImageCache`, scheduler 32 MB put / 32 MB rent): issue10717 and end-and-back.

24. `issue10717` uses over 2 GB of memory: one 2556 × 2406 image and its soft mask drawn 73 times, each `DrawImageCommand` with its own decoder holding a full decoded copy (3 GB alive). The decoder is now one per `PdfImage`; measure again.
1. Find what keeps live memory growing page after page during the scroll.
2. `SnapColorConverterCache`: one entry per page (`PdfDeviceProfiles.FromPage` builds a new instance, the cache key compares by reference), never evicted (156 caches, 242 converters after the scroll).
17. `PdfSnapMemoryPool` keeps 180 MB of blocks after the end-and-back scroll (8 per size): scheduler 148 MB in 38 blocks up to 32 MB, renderer 32 MB.
20. End-and-back scroll (`endback` script: 620 × 400 down, 620 up, hold): 582 MB of heap objects, 395 MB live, 187 MB dead; measure with a full dump walked from its roots.
21. 214 dead blocks of 262,144 bytes after the end-and-back scroll, source not identified.
23. End-and-back scroll allocations on the large object heap (723 MB): decoded image tiles 228 MB, `PdfImageSamples` 89 MB, `PdfSnapMemoryPool` misses 100 MB, ICC parsing ~68 MB.
18. Large-object-heap gaps make up ~200 MB of the peak (322 MB live + 205 MB fragmentation at 16 s); `DOTNET_GCConserveMemory=5` peaks at 491 MB instead of 591.
19. Two byte arrays over 10 MB alive besides the PDF file, holder not identified (full-page roots?).
3. `SnapGlyphStencilCache`: one per typeface in the document's `CommandCache`, never evicted, no size limit.
4. `SnapScratchBuffer` working rows, edge list and clip buffer only grow; never shrunk on `Reset`.
5. Content items (holding `PdfImage`) and words kept for every visited page; content items to be cleared with the page content.
8. `PdfImageSamples` allocations not attributed to a caller (~70 MB at tile 1024).
9. `PdfSnapSurface.DrawGroup` allocating on the large object heap through an inlined frame (50–260 MB at tile 1024), not attributed.
12. ICC profiles parsed again while scrolling (`IccProfile.ParseLut8` 31 MB, `ClutTransform` 19 MB).
13. Full-page float roots of the tiler, thumbnails and frame (SNAP-TODO 4).
14. Gold short `analyze` at 33.8 s against 29.4 s recorded in SNAP-TODO: check whether the layer and pipeline changes slowed Snap.
15. The release of a knockout text object's group on a cancelled replay is not yet exercised by a run.
