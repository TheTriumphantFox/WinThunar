# WinThunar 0.6.2

Published [13 November 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.6.2)
for Windows 11 x64 from source `ed64527be00899c3e5c765b3a27242e7a0f7c631` (PRs #49, #50 and #51).
All five public binary digests, both updater feeds and the source tag match the verified build.
Project, assembly/file, manifest and release-script versions are aligned at 0.6.2 (four-part
version 0.6.2.0).

## Changes

**Browsing a folder no longer runs its programs** ([#49](https://github.com/TheTriumphantFox/WinThunar/pull/49)).
An audit on 10 November 2026 found that a folder the user only opens, such as an extracted ZIP or
a USB stick, could make WinThunar run a program it carried:

- **Git status badges.** WinThunar runs `git status` on each folder it opens, and a crafted
  `.git/config` could name a clean or process filter for Git to run. Git now runs with every
  filter defined in the repository's own config switched off. Filters from the system or user Git
  config, such as Git LFS, keep working. A repository whose filter name cannot be switched off
  gets no badges. Status skips submodules, whose settings are not checked, and Git may not fetch
  or contact a remote while reading badges.
- **Helper programs.** Git, PowerShell, Explorer and Windows Terminal were started by bare name,
  which Windows looks up in the current folder first. They now start from fixed Windows locations
  or from fully qualified PATH entries, and WinThunar switches its current folder to the Windows
  system folder once it has read its launch arguments. Starting `WinThunar.exe .` while another
  window is open now opens a new window rather than handing the path to the running one, which
  would have read it against its own current folder.
- **Git plugin.** Git Status, Git Diff and Git Log use the same protections. Git Diff no longer
  runs a repository's external diff or text conversion programs, and Git Log no longer runs a
  repository's signature verifier.
- **User plugins.** A plugin command given as a bare name was looked up in the browsed folder
  before PATH. It now runs the program found on PATH; a relative path, or a name only the browsed
  folder holds, is refused with a message.

**Responsive transfers.** Queued copies and moves run on a background worker while conflict
prompts stay on the window, so the window keeps responding during large transfers. Progress
updates are throttled, and the transfer panel shows progress for the current file with an
indicator while a transfer prepares. Each window keeps its own queue.

**Optional navigation animations.** **Animate folder navigation** in Preferences turns folder
navigation animations off. The choice applies to every open window and is remembered between
launches.

**Open dialogs follow theme changes** ([#50](https://github.com/TheTriumphantFox/WinThunar/pull/50)).
A dialog open in one window, such as Preferences or a rename, kept its old theme when the theme was
changed from another window. WinUI unloads a dialog shortly after showing it, and that ended the
dialog's theme subscription. The subscription now ends only when the dialog or its window closes.

Help > What's New summarizes the release.

## Verification

The release source on `main` passes **241 service tests**, **233 desktop checks** and **two
theme-restart checks**, with no checks bypassed.

The service tests include tests whose fixtures run plain Git afterward to show the
planted program would otherwise run: clean, process and required filters, a filter from an
included config file, an unoverridable filter name, the plugin's diff with an external diff
program and its log with a forged signed commit. Locator tests show relative and drive-relative
PATH entries are skipped and that a program planted in the browsed folder is not chosen for a
plugin. A probe showed shell execute choosing a planted `ping.exe` in the working folder over the
system one before the change.

The desktop smoke suite passes on `dc6776b` with no checks bypassed: 233 checks and two restart
checks, including its Git badge, transfer and navigation checks. Its theme check for an open
Preferences dialog following a theme change from another window had failed since before these
changes (`f119e8f`); #50 fixed the cause and added checks that a dialog keeps its theme
subscription while open and drops it when its window closes.

## Package and publication verification

The release build ran from clean merged `main` using `Publish-Release.ps1 -Version 0.6.2`.
Application assemblies match across the versioned ZIP, public portable ZIP and full package, the
new Git, helper-program and dialog-theme code is present, and checked smoke, fault and theme-check
test hooks are absent. Both MSI copies match, carry version 0.6.2.0, retain scope/shortcut choices
and repair/migration safeguards, and embed the native guard from the release build.

The **844,956-byte** 0.6.1-to-0.6.2 delta reconstructs all **528 archive entries** with matching
names, lengths and SHA-256 digests. All **29 historical update packages** retain their hashes.
All five public binary sizes and SHA-256 digests match the verified build. The modern feed offers
matching 0.6.2 full/delta entries, the compatibility feed offers the matching full package,
latest stable resolves to 0.6.2 and the tag points to the source commit above.

Evidence is in `artifacts/verification-0.6.2/`. These are source/package checks; an installed
upgrade to 0.6.2 remains unverified.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,495,296 | `139c2a099bda46867fda4de39b6e8eabe360aabb0db79dce9b9659bb1466cf45` |
| `WinThunar.App-win-Setup.exe` | 119,550,342 | `399ea894cc0a7c46a03a558071cd3906b96ccabf6ac4ec8e5163bb5261bc0612` |
| `WinThunar.App-0.6.2-delta.nupkg` | 844,956 | `c48f8bc2f7ddd543e54d5a3da82b236245e057f134f46349c6be6c072a29a524` |
| `WinThunar.App-win-Portable.zip` | 112,002,217 | `a65fb88dbf44c90a0501b5ccf539600f52a8a51e825b0245cef675be3950601c` |
| `WinThunar.App-0.6.2-full.nupkg` | 112,018,822 | `266f6e16fd700b90490d49fb83ae96bad555644e70c6e39fd7bebda83ccf23ba` |

## Limits

The working folder Explorer passes to the **Open in WinThunar** verb was not checked; the change
does not depend on it. The partial-clone fetch protection is a precaution, not a reproduced
issue. Git older than 2.26 cannot list filters by scope, so badges are skipped there. Installed
upgrade to 0.6.2, slow-network stability, notification delivery and manual screen-reader,
contrast and scaling review remain unverified.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy all-users migration remains refused.
