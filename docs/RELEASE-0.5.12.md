# WinThunar 0.5.12

Published [23 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.5.12)
for Windows 11 x64 from source `1629c72935ea1faf6075b36a36a660f834faca80` (PR #31).
All five public binary digests, both updater feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.5.12
(four-part version 0.5.12.0).

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

The release was rebuilt from clean merged `main` with `Publish-Release.ps1 -Version 0.5.12`.
Both MSI copies have matching hashes, version 0.5.12.0, scope/shortcut choices and the existing
maintenance/migration guards. The embedded native DLL matches the final source build; deliberate
failure actions are absent. All **17 historical update packages** remain unchanged. Applying the
**711,758-byte** 0.5.11-to-0.5.12 delta reconstructs all **528 archive entries** with matching
names, lengths and SHA-256 digests. Application assemblies match across the versioned ZIP,
public portable ZIP and full package.

All five public binary sizes/SHA-256 digests match. The modern feed offers matching 0.5.12
full/delta entries, the compatibility feed offers the matching full package, latest stable
resolves to 0.5.12 and the tag points to the source commit above. Evidence is in
`artifacts/verification-0.5.12/`. Builds/tests report NU1900 when NuGet vulnerability metadata
is unavailable; the release publish and package checks passed.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,470,720 | `6109c1498e567bb36cf18f8053644f0f3bc482ea91ce28c70b02e7f93b4f7776` |
| `WinThunar.App-win-Portable.zip` | 111,948,456 | `f9966f31666eb828d93ed22c6e996cf289d4859ed480c5ae03f2d34d4e170a8e` |
| `WinThunar.App-win-Setup.exe` | 119,496,582 | `90b2cae1c7d9b9cff368ce1f585303baa2e471f56ccc4a28612d4b8e3de1217b` |
| `WinThunar.App-0.5.12-full.nupkg` | 111,965,062 | `3d3541148160552fc4d5d022962eae3070166b7b2c3b50ff02b7999ad31ae5bd` |
| `WinThunar.App-0.5.12-delta.nupkg` | 711,758 | `e0017ed258e356c23ecf26745530ed61963d9808eeea880bacb878893da890de` |

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
before legacy maintenance. Legacy all-users migration remains refused. The
[24 October installed retest](PRIORITY-VERIFICATION.md#installed-public-0511-restart-now-retest-24-october-2026)
uses public 0.5.11's fixed Restart Now handler to update to public 0.5.12: both tabs and active
folder survive a secondary window's save, all 528 reconstructed entries and 524 deployed files
match, and view/bookmark/theme/document retention passes. This uses an explicitly seeded base
cache and offline feed; other installation contexts and public-network discovery remain open.

Physical hardware/network and interactive UAC checks remain deferred at the maintainer's request.
Manual accessibility, installed/portable notification delivery, Office indexing filters, broader
previews and slow-network/day-long endurance remain open.
