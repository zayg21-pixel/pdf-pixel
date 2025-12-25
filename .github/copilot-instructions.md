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
- BEFORE MODIFYING ANY EXISTING FILE: fetch and read its current contents fully, then apply minimal, targeted edits without wholesale rewrites. Do not overwrite user code unintentionally.

Formatting and compactness
- Never place multiple statements on a single line. One statement per line.
- Always use braces for conditional/loop bodies (even single statements).
- Avoid overly compact forms: no chained operations or control-flow on one line.
- Avoid expression-bodied members except for very simple, single-return members.
- Avoid mixing declarations, assignments, and returns on the same line.
- Break long argument lists and conditions across lines with consistent indentation.

Naming
- Variables, fields, and parameters: descriptive, camelCase.
- Properties, methods, events, classes, structs, enums, interfaces: PascalCase.
- Private readonly fields: _camelCase.
- Constants: PascalCase.
- Only use i/j/k for simple, short-lived loop indices; otherwise use descriptive names.
- Do not use abbreviations unless common and unambiguous.
- Use PdfTokens constants for all PdfDictionary key lookups.
- Public types and members must include XML documentation.

C# style
- Use expression-bodied members sparingly.
- Prefer pattern matching and null-coalescing operators for clarity.
- Use var for obvious types; otherwise specify the type explicitly.
- Always use braces for conditional/loop bodies.
- LangVersion: 12. Use and support all C# 12 features across targets unless constrained by specific TFM.

Error handling
- Fail fast on invalid input using guard clauses.
- Avoid empty catch blocks; add comment if ignoring is intentional.

Testing and diagnostics
- Write testable code; keep logic out of UI/IO boundaries.
- Add logging or comments for non-trivial operations.


These rules guide Copilot Chat and completions for this repo. Add specific examples below as the codebase evolves.