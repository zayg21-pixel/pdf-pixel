# 0.9.1 — Missing document and page level entries

## 1. Technical debt

| Item | Location | Work |
|---|---|---|
| Document init sequence | `PdfDocument`, `PdfXrefLoader`, `PdfXrefRecoveryScanner` | Clean up the loading sequence: header, xref chain, `/Root` resolution and the recovery fallback. The recovery scanner sets the root on its own, so every caller re-checks the root after a lookup to let the scanner win. Decide where and when the root is resolved and whether the scan applied, in one place |
| General | All projects | Track public API changes
