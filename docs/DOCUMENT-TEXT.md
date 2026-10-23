# DOCX text previews and search

Included in published **[0.5.12](RELEASE-0.5.12.md)**. See the release notes for package verification.

Enable **View > Preview Pane**, then select a downloaded `.docx` file to read its body text.
The preview supports selection and copying and labels the result **DOCX body text**. It reads
text rather than rendering Word's page layout. It releases the source handle before parsing,
and selecting another item cancels the old preview and clears its content.

Use `type:docx content:"annual report" content:approved` to search the current body text.
All content terms must match, ignoring case. Adjacent formatting runs are joined, so a phrase
can match across runs. Paragraphs and explicit line breaks produce newlines; tabs and table
cell boundaries produce tab characters. This filesystem search uses the file's current bytes
without an Office installation or Windows content filter. An explicit `index:true` query still
uses Windows Search and retains its coverage/freshness limits.

The reader resolves the package's internal main-document relationship and accepts the
Transitional and Strict WordprocessingML namespaces. It extracts stored `w:t` text from the
main body, including ordinary table cells and stored field results. It excludes tracked deletions,
move-from content and field instructions. The package/paragraph/run/text structure is documented
by [Microsoft](https://learn.microsoft.com/en-us/office/open-xml/word/structure-of-a-wordprocessingml-document).

Limits are explicit:

| Resource | Limit |
| --- | --- |
| DOCX package | 16 MiB |
| Main document XML after decompression | 8 MiB |
| Root relationships and content-type metadata | 128 KiB each |
| Package entries / document XML depth | 4,096 / 128 |
| Preview output | First 32,768 characters, with a truncation notice |
| Content-search output | 1,048,576 extracted characters, including separators |

Documents over the search limit count as skipped, even when a term occurs in the retained prefix.
Malformed, encrypted, locked, unsupported or oversized documents also count as skipped during
content search; preview shows an unavailable message. Parsing validates the entire bounded main
XML even when the preview text is truncated. External main-part targets, DTDs, duplicate required
parts, escaping package paths and renamed macro-enabled main documents are rejected. The reader
does not extract package files to disk, resolve external resources or execute document content.
Links, offline files and cloud recall placeholders are excluded before reading.

This is body-text coverage, not full Office support. It does not render formatting or images,
calculate fields, extract separate headers/footers/comments/footnotes, or support password entry,
legacy `.doc`, `.docm`, spreadsheets or presentations. More complex layout/alternate-content
documents and representative files from additional editors remain to be reviewed. Windows Search
filter registration and generated Office indexing coverage remain separate open items.

Verification uses generated packages, with no copied Office documents. Service regressions cover
Unicode, split runs, tables, breaks, Strict and relocated/root-relative main parts, fresh search
after edits, preview/search limits, cancellation, locks/handle release, malformed/duplicate/missing
parts, external/escaping paths, DTD refusal, content-type checks and placeholder/link exclusion.
The isolated desktop suite checks direct search, fresh edits, the read-only preview, accessible
name, truncation, source-handle release, malformed-input cleanup and selection cancellation.
