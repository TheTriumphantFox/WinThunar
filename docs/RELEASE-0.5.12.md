# WinThunar 0.5.12

Prepared for Windows 11 x64. Publication is pending; installed users still receive 0.5.11 until
the GitHub Release and updater feed are published. Project, assembly/file, manifest and
release-script versions are aligned at 0.5.12 (four-part version 0.5.12.0).

## What's new

**Read DOCX body text in the Preview Pane.** Select a downloaded DOCX file to read, select and
copy its stored body text without Office. The preview has an explicit body-text label, a
truncation notice and an accessible name. Selection changes cancel pending previews and clear
old content. The status now appears above preview content, including PDFs and ordinary text.

**Search current DOCX text.** `type:docx content:"annual report" content:approved` searches
the current document bytes without an Office filter or Windows indexing. Phrases can span
formatting runs. All terms must match, ignoring case. An explicit `index:true` query continues
to use Windows Search with its existing coverage/freshness limits.

**Reduce thumbnail allocation.** Image work uses at most six workers per batch, with the
existing six-call gate across overlapping batches. Equivalent Details/Compact and text-zoom
requests reuse an unchanged entry's image. Size/content-mode changes and changed file metadata
still refresh it. Superseded requests cannot overwrite the current view.

Help > What's New describes the 0.5.12 additions and their main limits.

## Verification

The pre-release service suite passes **211 tests**, with one existing symbolic-link permission
skip and no failures. The final 0.5.12 isolated desktop suite passes **85 checks and two
theme-restart checks**, including the release summary in Light and Dark. DOCX, PDF and
What's New renders were inspected. The search/stress
project and a normal x64 publish also passed; test-only entry points and fixture helpers are
excluded from normal packages.

The five-minute thumbnail follow-up completed **501 cycles**, **1,503 searches** and **1,002
previews**, with a maximum observed dispatcher gap of **296 ms**. Allocation averaged about
**15.76 MiB per cycle**, versus **19.81 MiB per cycle** in a fresh 60-second original-build run.
Different durations and the short baseline do not establish flat memory use or a complete
long-session memory remedy. See [priority verification](PRIORITY-VERIFICATION.md).

Release package, historical-package retention, MSI policy, delta reconstruction, public asset/feed
and source-tag checks will be recorded after the clean merged-source build and publication.

## Scope and remaining limits

DOCX support extracts stored main-body text, not Word page layout. The reader caps packages at
16 MiB, main-document markup at 8 MiB, preview text at 32,768 characters and searchable text at
1,048,576 extracted characters. It also bounds metadata, entry count and nesting. Oversized or
unsupported content counts as skipped during content search; preview reports unavailability.
Headers, footers, comments, footnotes, formatting, images, field calculation, password entry,
legacy DOC, DOCM and other Office formats remain outside this reader. Generated fixtures cover
Transitional/Strict namespaces, alternate main-part paths, malformed packages, limits, locks,
cancellation and refusal of external targets/DTDs. Broader editor/format coverage remains open.
See [DOCX support and limits](DOCUMENT-TEXT.md).

Automatic app updates **do not replace an older cached MSI**. Use the latest MSI for installer
safeguards. Legacy per-user MSI migration still requires administrator elevation in the
**same Windows account**; preserve an **external backup** of the complete user-state directory
before legacy maintenance. Legacy all-users migration remains refused. An update initiated by
0.5.11 uses its fixed Restart Now handler, but that released-handler installed path has not yet
been retested against 0.5.12; earlier source and installed update evidence remains separate.

Physical hardware/network and interactive UAC checks remain deferred at the maintainer's request.
Manual accessibility, installed/portable notification delivery, Office indexing filters, broader
previews and slow-network/day-long endurance remain open.
