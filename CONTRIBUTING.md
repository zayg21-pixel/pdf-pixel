# Contributing to PDF Pixel

Thanks for helping improve PDF Pixel. This page covers how to report problems, how to build and test the code, and what a pull request needs.

## Bugs and enhancements

A **bug** is wrong output or a failure: a page that renders incorrectly, content that is missing, an exception, or a document that never finishes rendering. An **enhancement** is anything that makes correct behavior better, such as faster rendering, lower memory use, or a feature that is not supported yet. Performance reports are enhancements; include the time or memory you measured and a way to reproduce it.

## Reporting a bug

Open an issue and include:

- **The PDF.** Attach it, or a reduced copy that still shows the problem. Without a file most rendering bugs cannot be reproduced.
- **The page number** and what is wrong on it; a screenshot of PdfPixel's output next to the expected one helps.
- **The PdfPixel version** or commit, and the platform.

Acrobat is the reference for how a PDF should look. Other viewers may render a malformed file differently, and that alone does not make PdfPixel's output wrong.

Security issues are the exception: follow [SECURITY.md](SECURITY.md) and do not open a public issue.

## Building and testing

You need the .NET 8, 9 and 10 SDKs. From the repository root:

```bash
dotnet build PdfPixel.slnx --configuration Release -warnaserror
dotnet test PdfPixel.Tests/PdfPixel.Tests.csproj --configuration Release
dotnet test PdfPixel.Jpg.Test/PdfPixel.Jpg.Test.csproj --configuration Release
```

CI runs the same steps on every pull request.

## Code style

The style is enforced by Roslynator analyzers and `.editorconfig`, both part of the build. CI treats warnings as errors, so a pull request must build without any.

## Rendering regressions

`PdfPixel.Gold` compares rendered pages pixel by pixel against golden snapshots. Its corpus is listed in [PdfPixel.Gold/gold.json](PdfPixel.Gold/gold.json): each entry names a public URL and a SHA-256, and no PDF is stored in this repository. The first argument is the working directory, which holds `gold.json` and the `Source`, `Snapshots` and `Inspect` folders.

Take the goldens on `main`, then compare your branch against them:

```bash
dotnet run --project PdfPixel.Gold -c Release -- PdfPixel.Gold setup
dotnet run --project PdfPixel.Gold -c Release -- PdfPixel.Gold generate
git checkout my-branch
dotnet run --project PdfPixel.Gold -c Release -- PdfPixel.Gold compare --inspect
```

`setup` downloads the corpus once (about 700 MB) and skips files already present. `compare --inspect` collects every differing page, with its golden, result and diff images, into `PdfPixel.Gold/Inspect`. Without `--full`, runs cover the short set; `--full` adds the slow documents. Run `dotnet run --project PdfPixel.Gold -c Release -- --help` for every command.

Font rendering can differ between machines, so a difference on a text-heavy page after a change that cannot affect text is usually environmental. Mention such pages in the pull request rather than regenerating goldens.

## Pull requests

- Keep each pull request to one topic.
- Add tests for changes to decoders and parsers.
- Back performance claims with a reproduction or a benchmark.
- Describe what changed and why in the pull request, not in code comments.
- Mention the Gold compare result for changes that affect rendering.

## License

By contributing, you agree that your contributions are licensed under the [MIT License](LICENSE).
