# Actions raised to the host

The panel performs go-to, named and set-OCG-state actions itself. Every action that leaves the document is raised as an event with the action, and the host decides what to do with it. GoToE and Launch currently fall to `PdfGenericAction` and are dropped.

## 1. Library (`PdfPixel/Actions/Model`)

Both types are added to the `PdfAction` factory switch, replacing the `PdfGenericAction` fallback for their action types.

| Type | Spec | Members |
|---|---|---|
| `PdfGoToEmbeddedAction` | 12.6.4.4 | `FileSpecification` (`/F`, optional), `Destination` (`/D`), `NewWindow` (`/NewWindow`), `Target` (`/T`) |
| `PdfEmbeddedTarget` | Table 206 | `Relationship` (`/R`: child or parent), `Name` (`/N`), `Page` (`/P`: page index or named destination), `Annotation` (`/A`: annotation index or `/NM` string), `Target` (`/T`, next target in the chain) |
| `PdfLaunchAction` | 12.6.4.6 | `FileSpecification` (`/F`), `NewWindow` (`/NewWindow`), `Windows` (`/Win`: file name `F`, default directory `D`, operation `O`, parameters `P`) |

## 2. Panel core (`PdfPanelActions`)

| Event | Action |
|---|---|
| `EmbeddedDocumentRequested` | `PdfGoToEmbeddedAction` |
| `LaunchRequested` | `PdfLaunchAction` |

Existing events stay: `UriRequested`, `RemoteDocumentRequested`, `NamedActionRequested`.

## 3. WPF (`WpfPdfPanel`)

- The panel no longer opens URIs: `OnUriRequested` and `LaunchUri` move to the demo.
- CLR events `UriRequested`, `RemoteDocumentRequested`, `EmbeddedDocumentRequested`, `LaunchRequested` and `NamedActionRequested` re-raise the `PdfPanelActions` events with their event args. They are subscribed again whenever the context is recreated.
- The empty GoToR handler and its TODO are replaced by the re-raised event.

## 4. Demo (`PdfPixel.Demo.Wpf`)

Every file opens in its default application; destinations inside the opened file are ignored.

| Source | Handling |
|---|---|
| URI | Opens http and https links (logic moved from `WpfPdfPanel`) |
| GoToR, Launch | Resolves `/UF` or `/F` relative to the folder of the open document and opens the file through the shell |
| GoToE | Writes the embedded file to a temp folder and opens it |
| Attachments tab | Clicking an item writes its embedded file (`UnicodeEmbeddedFile`, else `EmbeddedFile`, decoded with `PdfObjectStream.DecodeAsMemory`) to a temp folder under `PdfAttachmentViewModel.Name` and opens it, the same path as GoToE |

Document stream access is not thread safe: embedded file content is decoded under `lock (pdfPanel.Pages.DocumentLocker)`, and the temp file is written and the process started after the lock is released.

## Not in scope

- Other generic actions (Thread, Sound, Movie, Hide, forms, JavaScript and others)
- Web panel
