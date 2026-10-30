# Forms and default appearance

Widget annotations and form fields, one control type at a time, drawn with the default appearance (DA) primitives the FreeText fallback already uses.

Scope: widget annotations. Redaction annotations are out of scope. Full rich text is out of scope.

Test file: `PdfPixel.Tests/AnnotationFallbackTest.pdf` (page 2 holds the FreeText cases).

Acrobat is the reference for anything the spec leaves to the implementation. PDF.js barely supports DA and is not a reference here.

## Terms

- **DA (default appearance)**: a string of content stream operators, for example `/Helv 12 Tf 0 0 1 rg`. It may hold only the operators allowed inside a text object; the font operator (`Tf`, font name and size) is required by the spec but missing in real files. The grammar is the same wherever DA appears: form, field, FreeText.
- **AP (appearance stream)**: the annotation's own drawing, a form XObject per look (normal, rollover, down), with named states for check boxes and radio buttons.
- **V (value)**: the field's current value.

Prefer full names over PDF key acronyms in code, comments and docs; DA, AP and V are the only acronyms used freely.

## Rules

### Two render paths

- An annotation with an AP is drawn from the AP. Neither DA nor V changes how an AP is drawn; for check boxes and radio buttons the state inside the AP is picked by the appearance state (`AS`), not by V.
- Without an AP, or when the form sets "need appearances" (`NeedAppearances`, deprecated in PDF 2.0 but still read), the annotation is drawn by a fallback.
- AP is interactive: it carries its own rollover and down looks. Fallback is static: one look, used for static rendering and printing. Editing is planned as native controls (WPF/MAUI/Web) drawn on top of the page, which hide the fallback.
- Fallback rendering is not spec-defined: it is an approximation of Acrobat, not a conformance target.

### Interactive form and default resources

- `PdfInteractiveForm` (`IPdfDocument.InteractiveForm`, null without `AcroForm`) holds the document-level form data: default resources (internal `PdfResourceNode`, no parent), "need appearances", the document-wide DA string and text alignment, and the signature flags. The root fields are added in step 1.
- Default resources (`DR`) exist only on the interactive form dictionary. They are the pool every DA resolves its names against: FreeText, fields and widgets alike. A fallback never uses the page's resources, and an AP never uses the default resources.

### Default appearance

- DA is kept as a `PdfString` on its owner. There is no parsed DA model.
- `PdfAppearanceRenderer.CreateState` interprets it at draw time, as the spec's appearance stream construction does (12.7.4.3): a throwaway `PdfContentHost` over the default resources (the document's empty resources when there are none, `StructuralParents` null), run through the existing graphics state, color and text operators.
  - Only operators allowed in a text object are run; the rest (`q`, `Q`, `cm`, text showing, paths, XObjects) are skipped.
  - The state starts with Helvetica at 12, which a DA without `Tf` keeps.
  - A `Tf` name missing from the default resources becomes a named font.
  - A DA `Tm` keeps its scale and skew; the layout replaces its translation.
- The resulting `PdfGraphicsState` is also what native controls will read (a `GetDefaultAppearance` export, with the form host and page).

### Fonts and text

- Text is drawn from Unicode, never encoded into a font's codes: `PdfNamedFont.Shape` shapes it and `PdfRenderer.DrawTextSequence` draws it.
- `PdfNamedFont` is a font known by name only, cached per name by `FontProvider.GetNamedFont`. It uses the bundled standard 14 typeface for its name and substitutes characters the typeface lacks. Acrobat's short names (`Helv`, `HeBo`, `TiRo`, `Cour`, `Symb`, `ZaDb` and the rest) map to the standard 14 fonts.
- A font found in the default resources is drawn through the named font of its `BaseFont`; its embedded program is not used.

### Appearance primitives

`PdfAppearanceRenderer` (`Forms/Rendering`) renders the primitives every fallback is composed from: `CreateState`, `DrawBackground`, `DrawBorder`, `DrawText`. They take the border style (null means solid, width 1) and border effect; colors come from the caller.

Layout rules, checked against Acrobat:

- Text is padded by twice the border width inside the box.
- The first baseline is one ascent below the padded top; the line height is `TL` when set, otherwise ascent minus descent.
- Lines wrap between words; a word wider than the box breaks between characters. Text is not clipped.
- Each line is aligned by the text alignment (`Q`). Acrobat ignores `Q` and the DA `Tm` on FreeText; we follow the spec.
- The background follows the border path, cloudy border effect included.

### Field inheritance

- Inherited field values are resolved at load, top-down, the same way the page tree resolves its inherited attributes (`PdfPageResources`). Fields keep only their effective values; nothing walks from a child to its parents at use time.
- The chain starts at `PdfInteractiveForm` (document-wide DA and text alignment) and continues through the field tree, which can be any depth (`Kids`). Dotted full names are built in the same pass. Guard against cycles.
- A key present on a node replaces the inherited value whole. DA strings are never merged operator by operator. Example: the form has DA `/Helv 12 Tf 1 0 0 rg` (red) and a field has DA `/TiRo 12 Tf` (no color): the field draws in Times, black.

### No style object

- A compiled style type (DA plus border, background, alignment and flags, handed to native controls and fallbacks) was considered and rejected. After load-time inheritance the field and widget models already hold every effective value. Fallbacks and native controls read the models directly.

### Existing widget and field code

- `PdfWidgetAnnotation`, `PdfFormField` and the field classes are about a year old, unused, and are reworked fully. Do not treat their parsing or inheritance as a reference, and do not preserve their API.
- The extended interaction code (keyboard and mouse interaction interfaces, pointer state, key types in `Forms`) is removed.

### Rejected designs, do not revisit

- **A parsed DA model** (typed properties, a DA parser, ExtGState applied onto the model). Every color operator form needs its own shape, and the parser duplicates the operators. The DA is run through the operators instead.
- **Encoding text into a default resources font's codes**, or synthesizing an encoding for it. Text is shaped from Unicode with the named font.

## 1. Widgets and fields refactor

- Root fields on `PdfInteractiveForm`; field tree inheritance as described above. Inherited values: field type, field flags, V, default value, DA, text alignment, maximum length.
- Terminal fields and their widgets: a field merged with its widget in one dictionary, and fields with separate widget kids.
- Remove the extended interaction code.
- Check in Acrobat whether a DA on a separate widget kid is honored; if it is, it is one more step at the end of the chain.

Files: `PdfPixel/Forms`, `PdfPixel/Annotations/PdfWidgetAnnotation.cs`, `PdfPixel/Annotations/PdfAnnotation.cs`

## 2. Control types, one at a time

One fallback per control type, composed from the appearance primitives: text field, check box, radio button, push button, choice. Check box is a candidate for full support without the native overlay.

To measure in Acrobat as each control comes in:

- Border and background colors from the appearance characteristics (`MK`: `BC`, `BG`).
- Beveled and inset borders: the lighter and darker bands inside the border, and whether the text padding grows for them. Acrobat ignores both on FreeText.
- Auto size (font size 0): FreeText draws no text, but fields size the text to the box.

## Known, later

Added one at a time, each as its own small step:

- Rotation of the widget content
- Text field flags: multiline, comb, password
- Push-button captions and icons, including rollover and down
- Highlighting mode (`H`)
- Native control overlay, with the DA export it reads
