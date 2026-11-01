# Color spaces

The color space model (`PdfPixel/Color/ColorSpace`): what every color space family reads from the file, what it exposes, and how it renders.

Scope: the families of ISO 32000-2 8.6 (device, CIE-based, special). Output is sRGB.

Test file: `PdfPixel.Tests/CalibratedColorTest.pdf` (pages 1-8 CalRGB and CalGray, 9-14 Lab). Its header comment holds the layout rules for adding pages.

The spec decides wherever it defines the behavior. Acrobat is the reference only where the spec leaves the choice to the PDF processor. PDF.js and the other open viewers are not references for color.

## Rules

### Calibrated spaces (CalGray, CalRGB)

- `PdfCalibratedColorSpaceConverter` holds the white point, black point and the transform; `PdfCalibratedGrayColorSpaceConverter` and `PdfCalibratedRgbColorSpaceConverter` add their own gamma (and matrix for RGB).
- CalGray is XYZ = WhitePoint × A^G, built as CalRGB with the matrix diag(WhitePoint).
- The white point is adapted to D50 with Bradford.
- BlackPoint is the media black: the components decode into [BlackPoint, WhitePoint], then ISO 18619 black point compensation maps the neutral black at the black point's lightness, at most L* 50, to zero. AbsoluteColorimetric skips the compensation. Any neutral black point darker than L* 50 renders the same as none.
- `UseBlackPtComp` does not apply to CIE-based spaces; only ICC conversions read it.

### Lab

- WhitePoint is the ICC media white: the relative intents scale XYZ by D50 / WhitePoint (identical to reading the values as D50 Lab); AbsoluteColorimetric keeps the unscaled XYZ.
- BlackPoint is not applied.
- Sample values 0..1 (images without `Decode`, Indexed palettes over Lab) span L* 0..100 and the `Range` of a* and b*.

### Exposed model

- Every entry the file defines is exposed on its color space; values the converter falls back to are exposed as the value actually used (a missing `Matrix` is the identity, a missing `WhitePoint` is D65).
- The XML summary names the PDF key or array operand: "Gets the … (/WhitePoint), or … when absent." Array operands keep the spec's operand name: (base), (hival), (alternateSpace).
- Property and parameter names match; abbreviated names are expanded (`hival` → `HighestIndex`); class names never abbreviate (`Calibrated`, not `Cal`).
- Number arrays from the file stay `float[]` in PDF order; component ranges are `PdfRange[]`. No `PdfPixel.Color` type appears in the exposed model.
- Color spaces are created only inside PdfPixel: constructors are internal, without summaries.

### Rejected designs, do not revisit

- **Applying the black point in output RGB** (Acrobat's form for tinted black points). It makes the result depend on the output space.
- **Clamping the black point at L* 127/255 · 100**, the 8-bit Lab value that fits Acrobat within 1 level. It has no source; L* 50 is within 2 levels.
- **Reproducing Acrobat's broken tinted black points** (a cool black point renders red in Acrobat).

## 1. Separation and DeviceN special names

ISO 32000-2 8.6.6.4 and 8.6.6.5; nothing reads the colorant names today.

- Separation `/None`: paints nothing; `alternateSpace` and `tintTransform` are ignored.
- Separation `/All`: on an additive output the tint is complemented, gray `1 - tint`; `alternateSpace` and `tintTransform` are ignored.
- DeviceN whose names are all `/None`: paints nothing and never reverts to the alternate space. A DeviceN that mixes `/None` with other names stays as is: the alternate space receives those components.
- Add test pages for all three.

Files: `PdfSeparationColorSpaceConverter.cs`, `PdfDeviceNColorSpaceConverter.cs`, and wherever painting can be skipped.

## 2. ICCBased Range and Metadata

- Parse and expose `/Range` (`PdfRange[]`, default [0 1] per component).
- Images without `Decode` use `Range` as their decode (Table 88), as Lab already does.
- Expose `/Metadata`, the way other model types expose their metadata stream.

Files: `PdfColorSpaceResolver.Factory.cs`, `PdfIccColorSpaceConverter.cs`

## 3. DeviceN attributes

Parse and expose the attributes dictionary (Table 70): `Subtype` (DeviceN, NChannel), `Colorants` (name → Separation space), `Process` (`ColorSpace`, `Components`), `MixingHints` (`Solidities`, `PrintingOrder`, `DotGain`). Rendering keeps the alternate space and tint transform, which the spec always allows.

## 4. How color spaces reach the user

Today the converters are public, but nothing in the public model returns one:

- `PdfImage.ColorSpaceReference` and `PdfShading.ColorSpaceReference` are internal; `PdfTransparencyGroup.ColorSpaceReference` is public but cannot be resolved by a user.
- Resolution goes through the internal `PdfResourceNode.ResolveColorSpace`, which also applies the `DefaultGray`, `DefaultRGB` and `DefaultCMYK` substitutions of the resources in effect.
- Users see only resolved colors: `PdfPaint.Color` in the recorded commands, `PdfColor` on annotations, outline items and shading color stops.

To decide: whether images, shadings and groups expose their resolved color space, and against which resources it is resolved.

## 5. Image pipeline shape

All image decoding runs on `PdfImageRowProcessingPipeline` (`Imaging/Processing/Pipeline`): one pipeline per tile, built by an `IPdfImagePipelineFactory`; `SkiaImagePipelineFactory` builds the Skia routes (stencil, direct gray/RGB, lookup, color). The shape is a first cut and gets a cleanup round.

Rules that stay:

- Steps are atomic and own their buffers; a pipeline belongs to one tile and one thread.
- Packed input is cropped to start at bit 0 (`PdfImageRowCropStep`), so no later step reads from an arbitrary pixel.
- Resampling is float in, float out (`PdfImageRowBoxAverageStep`, `PdfImageRowNearestNeighborStep`), after unpack or lookup.
- Fast paths only for the common depths: unpack 1 bpc (eight-float lookup block) and 8 bpc (vectorized), pack 8 bits (vectorized). Every other depth uses the bit reader or writer; depths above 8 are too rare to optimize.
- Color key compares raw codes before Decode and before resampling; Matte is undone before color conversion, in the 0..1 domain mapped through the inverse Decode; a soft mask overrides a color key (Table 87).
- The default Decode follows Table 88: Indexed `[0 2^bpc−1]`, otherwise the color space's `ComponentRanges`, otherwise `[0 1]`.

Remaining work:

- Decide whether `ImageDecodingContext` survives now that the decoders hand over one image paint (`PdfImageRowDecodingParameters.ImagePaint`).
- Remove the temporary crutches: `PdfImage.DecodePixels` creating a `SkiaImagePipelineFactory` (core must not depend on Skia), and the factory's null-context path that builds an uncached `SkiaColorSampler`.
- Replace the flag parameters (`hasAlpha`, `producesMask`, the factory's packed-alpha handling) with a clearer shape.
- Resolve a missing `/ColorSpace` from the component count the data defines, never a fixed device space: JPX from its header (Table 87 allows absence only there), JPEG from its SOF instead of DeviceRGB, CCITT and JBIG2 as 1 component; raw images have no count and are not drawn. `RawImageDecoder` guessing DeviceGray at 1 bit and DeviceRGB otherwise, and the resolver's `defaultComponents = 3`, go away.
- Split `IPdfImagePipelineFactory`: `CreateImagePipeline` and `CreateMaskPipeline` become two implementations of one factory interface, and function-based shadings get a third that takes their float samples. Until then `PdfShadingConverter.ToSkImage` builds the shading's color steps itself.

## 6. Color sampling cleanup

Colors convert at render time: a converter returns its chain to the profile connection space (`GetProfileConnectionTransform`) or null for device and special spaces; `SkiaColorSampler` builds a `PdfColorSampler` from a `PdfPaint` (palette and tint special cases, device replacement by component count, PCS → sRGB, transfer function as its own step) and the command cache keeps it per paint. Images take the same cached sampler through the image paint.

Remaining work:

- Drop `ToSrgb` and the per-converter sampler cache: samplers live in the page's cache, built from the paint. Move `PdfPaint.RgbColor` users (stencil paint color, mask paint, annotations) to the sampler.
- Remove the old sampling path: `GetRgbaSampler`, `GetRgbaSamplerCore`, `ColorTransformSampler`, the `normalize` flag, `SpanConverter`, the sRGB tails of Cal, Lab and ICC with `IccProfileTransform.GetIntentTransform`, `IndexedColorTransform`, `BuildPalette` and `BuildPackedPalette`.
- `TransferFunctionTransform` stops implementing `IColorTransform` once nothing chains it.
- The CMYK profile is parsed twice (`PdfDeviceCmykColorSpaceConverter` and `SkiaColorSampler`); keep one.
- Black point compensation still compensates toward a zero black inside the source half. Its join between the source and destination halves gets a shape when a target needs it.
- CMYK targets are additive, not part of this cleanup: black generation and undercolor removal on `PdfPaint`, DeviceRGB → DeviceCMYK (10.3.5) and DeviceGray → DeviceCMYK (10.3.4).

## 7. Output intents (undecided)

Today `PdfOutputIntent.CreateConverter` picks one catalog output intent (PDF/X, then PDF/A, then PDF/E) and `PdfResourceNode.ResolveDeviceColorSpace` uses its profile as the fallback `DefaultGray`, `DefaultRGB` or `DefaultCMYK` of every resource node.

What is wrong with it:

- The output intent describes the target device (Table 401, `DestOutputProfile`), not the source; the default color spaces describe source colors, and the output intent supplements them (14.11.5). Inside `PdfDeviceColorMapping` it is applied before the target is known, so a CMYK target matching the output intent cannot pass device CMYK through (8.6.5.7).
- Choosing among several output intents is the PDF processor's workflow decision (14.11.5), not the model's.
- Page `/OutputIntents` (PDF 2.0, Table 31) is not read. When present, it replaces the catalog's for that page (14.11.5).

Direction: the mapping holds only the resources' default spaces; the output intent profile becomes the device profile `SkiaColorSampler.CreateDeviceTransform` uses for its component count in place of the built-in one, with the selection next to it.

Why it is parked: page-level intents make rendering depend on the page.

- Resource nodes of forms, tiling patterns, Type 3 fonts and `/DR` are shared across the document, so `PdfDeviceColorMapping` cannot carry page intents.
- Content hosts are created per use and could carry `page ?? document` down; `PdfTilingPattern.CellRecording` (one slot per pattern object, reused on every page) would then need the intents in its reuse condition.
- Resolving at replay instead needs the page on `PdfCommandExecutionContext`, passed by every host.

Output intents are the only reason for a page dependency today; decide when a target needs them.

The same host question applies to `PdfShading.GetContent`: mesh shadings without a function store one value per component of their color space, so building the content needs the resolved color space, and resolving it needs a host. The content is cached per resolved color space until that is decided.

## 8. Model colors converted at parse time

`PdfOutlineItem.Color` (/C) and the color attributes of `PdfLayoutAttribute` (BackgroundColor, BorderColor, Color, TextDecorationColor) are exposed as `PdfRgbaColor`, converted when parsed. They should expose the components as written, in the device space their count selects, and be converted only where they are drawn.

## Parked, unrelated

Gold differences found while validating this work but not caused by it; investigate on their own.

- `issue18030`, page 1
- `pr19449`, page 1
