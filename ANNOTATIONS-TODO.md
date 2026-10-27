# Annotation cleanups

Open annotation work, smallest first. Items that share work are grouped together. Items are tracked here only, with no `// TODO` in code; the listed files are where the work lands.

Widgets and the FreeText default style string are tracked in `FORMS-TODO.md`. Hide-action field targets are forms work and are not tracked here.

Test files: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (fallback rendering playground), plus the Gold entries `comments.pdf`, `highlights.pdf`, `issue14438.pdf`, `issue9552.pdf`.

## 1. Annotation flags

Every flag of ISO 32000-2 Table 167 is supported. Flags are done one at a time: add cases for the flag to `AnnotationFallbackTest.pdf`, implement, then compare against Acrobat.

Rules:
- Flags are viewer behavior and are handled by the panel only. Core annotation rendering ignores them and needs no view state.
- One panel predicate decides whether an annotation is drawn and whether it interacts. The annotation recording and hit testing both use it.
- Gold, Cli and Diagnostics keep their own Hidden/NoView filter and do not apply NoZoom or NoRotate.
- Text annotations behave as if NoZoom and NoRotate were always set.
- ReadOnly is ignored for widget annotations; the field's ReadOnly flag takes its place.
- Locked and LockedContents need nothing: they restrict editing, which the library does not have.
- Print has no implementation until something prints; the panel never does.

### 1.1 Panel visibility predicate (bug)

NoView annotations and Invisible annotations of unknown type are not drawn, but they still take hover, change the cursor, open their popup and run their action: `PdfAnnotationPopup.IsInteractive` checks only Hidden and optional content.

How: move the check in `GetAnnotationRecording` into one panel predicate (Invisible of unknown type, Hidden, NoView) and use it in `IsInteractive` as well.

Files: `PdfPixel.PdfPanel/Caching/PdfDocumentContentExtensions.cs`, `PdfPixel.PdfPanel/Annotations/PdfAnnotationPopup.cs`

### 1.2 ReadOnly

A ReadOnly annotation is drawn, but does not respond to clicks or change its appearance on hover: it is always recorded in the Normal visual state, is not interactive and runs no action. What else it blocks is open question 3.

Files: `PdfPixel.PdfPanel/Caching/PdfDocumentContentExtensions.cs`, `PdfPixel.PdfPanel/Annotations/PdfAnnotationPopup.cs`

### 1.3 NoZoom and NoRotate

The panel records a flagged annotation between `SaveState` and `RestoreState`, with a compensating matrix concatenated before `Render`. The recording is in unrotated page space (top-left origin), and the page rotation is applied only when the page is drawn.

- Pivot `p`: the upper-left corner of the annotation rectangle in default user space, mapped through the recording's page transformation (ISO 32000-2 12.5.3, Figure 78). In page space that is the top-left corner of the mapped rectangle.
- NoRotate: concatenate `T(p) · R(−θ) · T(−p)`, where θ is the request's page rotation. Drawn as `R(θ)`, the pivot stays in place and the annotation stays upright.
- NoZoom: concatenate `T(p) · S(1/s) · T(−p)`, where `s` is the request scale relative to the reference scale (open question 1).
- Both flags: apply both matrices around the same pivot.

Consequences:
- `NeedsAnnotationRecordingUpdate` has to re-record when the scale changes on a page with NoZoom annotations, and when the rotation changes on a page with NoRotate annotations.
- Hover rectangles are computed once, when `PdfAnnotationPopup` is created. For these annotations they have to be mapped through the same matrix for the current request.

Files: `PdfPixel.PdfPanel/Caching/PdfDocumentContentExtensions.cs`, `PdfPixel.PdfPanel/Caching/PdfPageCacheEntryItem.cs`, `PdfPixel.PdfPanel/Annotations/PdfAnnotationPopup.cs`, `PdfPixel.PdfPanel/Caching/PdfDocumentAnnotationExtractor.cs`

### 1.4 ToggleNoView

A NoView annotation with ToggleNoView is drawn, and interacts, while the pointer is over it or it is selected. The panel already has the active annotation and its pointer state; it has no selection state yet (open question 4).

Files: `PdfPixel.PdfPanel/Caching/PdfDocumentContentExtensions.cs`, `PdfPixel.PdfPanel/Annotations/PdfAnnotationPopup.cs`

### Open questions (verify in Acrobat)

1. NoZoom reference scale: which panel scale keeps the authored size (presumably 100%), and whether UserUnit counts as zoom or as page size.
2. Always NoZoom/NoRotate: the spec names only Text. Does Acrobat also treat FileAttachment and Sound icons, or Popup, that way?
3. ReadOnly reach: does it also suppress the popup on hover? Does a ReadOnly link still follow its link?
4. ToggleNoView "selected": what selection means in a viewer without annotation editing; keyboard focus only?
5. Pivot rectangle: `Rectangle` is overridden by computed bounds for fallback-rendered Line, PolyLine, Polygon, Ink and text markup, and by the default bubble size for a zero-size Text annotation. Which rectangle does Acrobat pivot on in those cases?
6. Gold, Cli and Diagnostics: confirm they render at the reference scale and without page rotation, so skipping NoZoom/NoRotate there still matches Acrobat.
