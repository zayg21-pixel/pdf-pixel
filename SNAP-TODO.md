# Snap TODO

## Before main

1. Move the default image decoding pipeline out of Snap into shared code with the color sampler; bring back `PdfImage.DecodePixels` / `DecodePng` on it.
2. Make `PdfPixel.Tests/Snap` compile; add the source-over vs general compositor test.
3. Check Snap against the Gold goldens.
4. PdfPanel tiler and thumbnails on 8-bit roots, like the frame: their float roots are 4× an 8-bit surface and need a `ToImageSamples` conversion; the tiler must keep replacing a tile's pixels atomically under its lock.
5. Soft-mask content never reaches the content tree (`IsLocal` keeps it out; the zero-size surface skips `SetSoftMask`), so `IncludeSoftMaskContent` has no effect.
6. Tiling pattern cells have no `Pattern` stream scope: in-place cells add page content once per cell position, window cells add none.
7. `DrawGroupContent` keeps an open text object: a `BT` inside group or pattern-cell content closes the outer one.
8. issue19624: Type 3 glyph bounds still computed wrong; an earlier "fix" removed the TODO without verifying.
26. Split the mega classes: `SnapLayer` (740 lines; result creation, rectangle math, row slicing and group color conversion are not layer work), `PdfSnapSurface.Image` (799), `SnapBlendFunctions` (558), `SnapRasterizer` (531), `SnapSourceOver` (466).

## Can wait

9. Device colors of objects (not groups) inside an ICC group: DeviceGray/RGB/CMYK values read as the nearest ICC ancestor space with as many components, not converted through the device profile (`SnapLayer.GetConverter`).
10. Transfer function applied only where the topmost object is fully opaque (alpha 1, Normal blend, no soft mask), the default one elsewhere.
11. Images: clamp Matte-recovered colors.
12. Quarter-turned images as the aligned case, in alignment (`GetImageArea`) and in sample placement, reading sample columns as rows.
13. Stroke adjustment (`SA`).
14. Output color space in the `PdfSnapSurface` constructor.
15. Fonts to substitute in the browser (Web panel), which has no system fonts.
16. Rendering options that turn features off for faster rasterization.
17. Sources behind one interface; shading pattern as a shading source without a group layer.
18. Performance against Skia (short `analyze`: Skia 27.4 s, Snap 29.4 s): 1-bit scans (issue1015, issue3999, issue10491, vesta, issue4872); pages without images (issue8019, issue4387, issue9418); paths (bug1791583); large inline image (bug1077808); other images (issue18208, issue1905, bug1703683_page2_reduced, bug1749563); files 0.02–0.1 s slower.
19. Type 3 image stencils rasterized once per quarter-pixel position.
20. Exact group reductions: opaque Normal knockout element as source over; all-Normal pattern as isolated.
21. Cache mask recordings.
22. Shading cache per execution context.
23. Cache source work repeated per tile (tiling period cells, shading grids).
24. `ExecuteCommandAsync` steps into recordings but not groups: a page group replays without yielding (thumbnails).
25. `PdfImageRowResizeStep`: integer sums for codes, float sums for floats, instead of `double` for every format.

## Decide later

- Glyph stencil cost on text-heavy pages (083014_hanrei); size limit for the glyph stencil cache.
- Transfer function through a lookup; 16-bit precision of the ICC TRC lookup.
- One model for the transfer function on `PdfPaint`.
- `PdfSnapGrid` tolerance from float precision; reason for `CoordinateLimit`.
- Child group shape only under a knockout ancestor.
- General vectorization of compositing for RGB and CMYK; storage layout.
- Gray and CMYK blend path tests.
- Type 3 glyph recordings cached.

## Ideas

- Page group as the surface's root.
- Output conversion in `ToImageSamples`.
- Surface owns its root space, output conversion and converter cache.
