# Copilot Instructions for this repository

Use these rules when generating or editing code in this repository.

General
- Prefer clarity over brevity. Favor readability and maintainability.
- Use descriptive, self-explanatory names for identifiers. Avoid cryptic or single-letter names.
- Keep methods focused and small; extract helpers when a method grows.
- Add XML documentation to public APIs and concise comments where intent isn’t obvious.
- Avoid magic numbers; introduce well-named constants.
- Prefer immutability and pure functions when possible.
- Preserve existing comments; update them if they become outdated instead of removing.
- NEVER remove TODO comments. Preserve every TODO exactly as written. If the surrounding code changes, you may add clarification after the existing TODO but must not delete or rewrite the original TODO marker or text.

Formatting and compactness
- Never place multiple statements on a single line. One statement per line.
- Always use braces for conditional/loop bodies (even single statements).
- Avoid overly compact forms: no chained operations or control-flow on one line (e.g., `if (...) break;` should use its own block when adding logic; avoid `return x ?? (y ? a : b)` for non-trivial logic).
- Avoid expression-bodied members except for very simple, single-return members; prefer full method bodies.
- Avoid mixing declarations, assignments, and returns on the same line.
- Break long argument lists and conditions across lines with consistent indentation.

Naming
- Variables, fields, and parameters: descriptive, camelCase (e.g., imageWidth, decodeParameters).
- Properties, methods, events, classes, structs, enums, interfaces: PascalCase (e.g., DecodeParameters, GetImageBounds).
- Private readonly fields: _camelCase (e.g., _cache, _logger).
- Constants: PascalCase.
- Only use i/j/k for simple, short-lived loop indices. Otherwise use descriptive names (e.g., rowIndex, columnIndex).
- Do not use abbreviations unless they are common and unambiguous (e.g., Uri, Id is fine; img, cfg, prm are not).
- Use PdfTokens constants for all PdfDictionary key lookups.
- Public types and members must include XML documentation.

C# style
- Use expression-bodied members sparingly for very simple members.
- Prefer pattern matching and null-coalescing operators for clarity.
- Use var for obvious types; otherwise specify the type explicitly.
- Always use braces for conditional/loop bodies.

Error handling
- Fail fast on invalid input using guard clauses.
- Avoid empty catch blocks; at least add a comment explaining why it’s safe to ignore.

Testing and diagnostics
- Write testable code. Keep logic out of UI/IO boundaries when possible.
- Add logging or comments around non-trivial operations.

Performance
- Avoid unnecessary allocations in hot paths.
- Use spans/readonly structs carefully and only when justified by measurements.

PDF-specific (project context)
- When decoding images, separate responsibilities: decompression, predictor undo, sample unpacking, color conversion, masking.
- Follow PDF spec ordering for image processing and document any deviations.
- ToUnicode/CMap: Prefer length-aware CIDs over numeric keys. Use PdfCid instead of uint for character code keys.
- Codespace ranges: Treat source bytes as raw, 1–4 bytes. Implement longest-match using ranges when available.
- usecmap: Cache and merge base CMaps before local overrides.

These rules guide Copilot Chat and completions for this repo. Add specific examples below as the codebase evolves.