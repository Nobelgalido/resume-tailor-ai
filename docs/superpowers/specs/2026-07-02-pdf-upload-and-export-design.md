# PDF Resume Upload + PDF Export Design

## Context

Today `Home.razor` only accepts pasted plain text/markdown for the resume and job
description (`TailorRequest.Resume`, `TailorRequest.JobDescription` — both plain
`string`). The tailored result is only viewable on-page (rendered markdown) or
copyable as raw markdown text.

This adds two independent, symmetric features:
1. **PDF upload** — let the user upload a PDF resume instead of pasting text.
2. **PDF export** — let the user download the tailored result as a PDF file.

A third feature discussed and explicitly deferred: web-search-grounded ATS/formatting
research feeding into the Rewrite prompt. That is a separate architectural change
(new external API, grounding calls, latency/cost per run) and will get its own
design later.

## Goals

- Resume-only PDF upload (not job description — job postings are pasted from
  webpages/emails, not PDFs).
- Extracted text populates the existing resume textarea so the user can review/edit
  before running the pipeline — never sent straight through unseen.
- "Download PDF" for the tailored result, styled as a clean single-column ATS-style
  document (matching the Rewrite prompt's existing "no tables or columns" rule).
- No changes to `TailorRequest`, `TailorResult`, `IResumeTailorPipeline`, or the
  `/api/tailor` endpoint — both features operate purely at the UI edges.
- No disk writes anywhere in either flow — consistent with the app's existing
  "nothing you paste is stored" privacy claim.

## Non-goals

- OCR / scanned-image PDF support.
- PDF upload for the job description field.
- Web-search-grounded ATS optimization (separate future spec).
- Automated tests (no test project exists in the repo today; verification is manual,
  consistent with the project's current state).

## Architecture

Two independent additions around the existing pipeline, both operating in-memory:

- **Upload (input):** Blazor `<InputFile>` → PDF bytes streamed into memory → text
  extracted server-side via PdfPig → populates `_request.Resume` in `Home.razor`.
- **Export (output):** "Download PDF" button next to the existing "Copy markdown"
  button → tailored markdown converted to PDF bytes via QuestPDF → browser triggered
  to save the file via a small JS interop helper.

## Components

### 1. PDF text extraction — `Services/PdfResumeTextExtractor.cs` (new)

- Static helper (or small injectable service, matching the style of existing
  services) with a method:
  `string? TryExtractText(Stream pdfStream, out string? error)`
  or an equivalent result type — exact shape decided during implementation planning.
- Uses **UglyToad.PdfPig** (MIT, pure managed, no native binaries — avoids the
  packaging class of problem hit with `libe_sqlite3` native runtimes during the
  Azure Linux App Service deploy).
- Opens the PDF, iterates pages, extracts text per page (`Page.Text` or
  `ContentOrderTextExtractor` for better reading order on multi-column resumes),
  concatenates pages with a blank line between them, trims the result.
- Returns `null`/error when: the file isn't a valid PDF, it's encrypted, or the
  extracted text is empty/whitespace-only (covers scanned/image-only PDFs).
- Never writes to disk; operates entirely on an in-memory stream.

### 2. Upload UI — `Components/Pages/Home.razor` (modified)

- Add an `<InputFile accept=".pdf" />` control above or beside the resume textarea
  (e.g. "Upload PDF" button/dropzone), disabled while `_running`.
- `OnChange` handler:
  - Reject non-PDF files (defense in depth beyond the `accept` attribute).
  - Enforce a 5MB max via `InputFileChangeEventArgs.File.OpenReadStream(maxAllowedSize: 5_000_000)`.
  - Read into a `MemoryStream`, call the extractor.
  - On success: set `_request.Resume` to the extracted text, clear `_error`.
  - On failure: set `_error` to a friendly message — *"Couldn't read text from that
    PDF — try pasting it instead."* — and leave the textarea untouched. Reuses the
    existing `.alert` error UI already in the page.
  - Wrap extraction in `Task.Run` (or keep it inline if fast enough — decide during
    implementation) since Blazor Server runs on a shared circuit and a slow
    synchronous parse would block the UI thread.

### 3. Markdown → PDF rendering — `Services/MarkdownPdfRenderer.cs` (new)

- Uses **QuestPDF** (Community License — free for individuals and companies under
  $1M annual revenue; not pure MIT, but acceptable for a personal portfolio project.
  Worth a one-line mention in the README given the repo is public).
- Takes the tailored markdown string, parses it with the same Markdig pipeline
  already used for on-page rendering (`Home.razor`'s `MarkdownPipeline`), and walks
  the resulting AST to build a QuestPDF document:
  - Headings → bold text at a larger size (heading level determines size step).
  - Paragraphs → normal text, preserving inline `**bold**`/`*italic*` spans.
  - Bullet lists → indented bullet items.
  - Single column, standard margins, no tables — matching the Rewrite prompt's
    existing ATS-friendliness rules.
- Returns a `byte[]` (the PDF file contents). No disk writes.

### 4. Download trigger — `wwwroot/js/download.js` (new) + `Components/App.razor` (modified)

- Small JS helper, e.g. `downloadFileFromBytes(fileName, contentType, base64Data)`,
  using the Blob + anchor-`download`-attribute pattern (the standard way to trigger
  a file save from JS since there's no built-in browser API for it).
- Referenced via a `<script src="js/download.js"></script>` tag in `App.razor`,
  alongside the existing `blazor.web.js` script tag.
- `Home.razor` gets a "Download PDF" button in the `result-meta` row (next to
  "Copy markdown"): on click, calls `MarkdownPdfRenderer`, base64-encodes the bytes,
  and invokes the JS helper via `IJSRuntime.InvokeVoidAsync` (same interop pattern
  already used for `CopyResult`/`navigator.clipboard.writeText`).
- Downloaded filename: `tailored-resume.pdf`.

## Data flow

**Upload:**
```
User picks file → InputFile (browser) → OnChange handler (server)
  → stream capped at 5MB, .pdf only
  → MemoryStream → PdfResumeTextExtractor
  → success: _request.Resume = extracted text, _error = null
  → failure: _error = "Couldn't read text from that PDF — try pasting it instead.", textarea untouched
```

**Export:**
```
User clicks "Download PDF" → MarkdownPdfRenderer(_result.TailoredResume) → byte[]
  → base64 → JS interop → downloadFileFromBytes() → browser saves tailored-resume.pdf
```

## Error handling

- Upload: wrong file type, oversized file, encrypted/corrupt PDF, or empty text
  extraction all resolve to the same friendly `_error` message and leave the
  textarea untouched — no exceptions surfaced to the user, no crashes.
- Export: if PDF generation throws unexpectedly, show an inline error near the
  download button (not a full-page failure) and log via the existing
  `ILogger<T>` pattern used elsewhere in the pipeline.

## Dependencies to add

- `UglyToad.PdfPig` (MIT) — PDF text extraction.
- `QuestPDF` (Community License, free under $1M revenue) — PDF generation.

## Testing / verification

No test project exists in this repo today. Verification is manual:
1. Upload a real PDF resume → confirm extracted text appears correctly in the
   textarea and can be edited before running.
2. Upload a non-PDF file and an oversized file → confirm friendly errors.
3. Upload a scanned/image-only PDF → confirm the "couldn't read text" error.
4. Run a full tailoring pass, click "Download PDF" → open the downloaded file and
   visually confirm headings, bullets, and bold/italic formatting render correctly
   in a single-column, ATS-style layout.
