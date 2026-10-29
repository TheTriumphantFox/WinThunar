# WinThunar 0.6.1

Published [29 October 2026](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.6.1)
for Windows 11 x64 from source `19a55e28609cfe0c7a1f23210dba9229f1c223df` (PR #46). All five
public binary digests, both updater feeds and the source tag match the verified build. Project,
assembly/file, manifest and release-script versions are aligned at 0.6.1 (four-part version
0.6.1.0).

## Changes

**System colors theme** ([#42](https://github.com/TheTriumphantFox/WinThunar/issues/42)).
**Edit > Preferences > Display > Theme** gains a fourth choice beside System, Light and Dark. It
follows the Windows light/dark setting like System and adds:

- The Windows 11 **Mica** backdrop. The menu bar, toolbar, location bar, side pane, status bar and
  transfer panel sit directly on it, so the desktop wallpaper tints the window.
- Translucent file panes, preview pane and command panel over the Mica, using Windows 11's layer
  fill. The selected tab takes the same fill and joins the pane below it.
- Selected files (Details, Icons, Compact and the split pane), selected folders in the tree and
  their hover/pressed states tinted with the Windows accent color at 16–26% opacity, so row text
  keeps its normal contrast. The tint follows accent changes made in Windows Settings while
  WinThunar is open.

System, Light and Dark look as before. Windows High Contrast keeps its system colors and solid
surfaces in every theme. The choice is stored like the other themes; an earlier version that reads
it falls back to System.

WinThunar originally planned to integrate matugen for this. matugen is GPL-2.0 and WinThunar is
MIT, so the theme uses the colors Windows already provides instead.

Help > What's New summarizes the release. CONTRIBUTING.md records the WinUI default colors the
theme copies and how to re-check them on Windows App SDK upgrades.

## Verification

The release source on `main` passes **215 service tests**, **219 desktop checks** and **two
theme-restart checks**. The theme checks now cover all four choices. For each theme, in both an
existing and a new window, they check the backdrop, the page and file-pane surfaces, and that
file rows draw their selection with the theme's shared brush; System colors must show Mica,
translucent panes and the current Windows accent. On a dark-mode Windows 11 desktop, window captures show the Mica backdrop, the translucent
panes, the selected tab joining the pane and an accent-tinted selected row.

## Package and publication verification

The release build ran from clean merged `main` using `Publish-Release.ps1 -Version 0.6.1`.
Application assemblies match across the versioned ZIP, public portable ZIP and full package, the
System colors code is present, and checked smoke, fault and theme-check test hooks are absent.
Both MSI copies match, carry version 0.6.1.0, retain scope/shortcut choices and repair/migration
safeguards, and embed the native guard from the release build.

The **706,427-byte** 0.6.0-to-0.6.1 delta reconstructs all **528 archive entries** with matching
names, lengths and SHA-256 digests. All **27 historical update packages** retain their hashes.
All five public binary sizes and SHA-256 digests match the verified build. The modern feed offers
matching 0.6.1 full/delta entries, the compatibility feed offers the matching full package,
latest stable resolves to 0.6.1 and the tag points to the source commit above.

Evidence is in `artifacts/verification-0.6.1/`. These are source/package checks; an installed
upgrade to 0.6.1 remains unverified.

| GitHub binary | Bytes | SHA-256 |
| --- | ---: | --- |
| `WinThunar.App-win.msi` | 93,454,336 | `ed0b50e350226dd72c0579ee728680b1f68d855b322dbb301f954f9514376dea` |
| `WinThunar.App-win-Setup.exe` | 119,531,120 | `2f345556ca976f8bf2b041b0774afb355198b1d42741f111fc0b58e1e7c7b318` |
| `WinThunar.App-0.6.1-delta.nupkg` | 706,427 | `0a94405e7de998813ad2f83018c0e862f8af83165b3f41b93ded276ea362aaff` |
| `WinThunar.App-win-Portable.zip` | 111,982,995 | `bd5ee82281cb5190b0ca067c1a4b28c09e1bb74d51baf1c8524a3a6f71fbcfba` |
| `WinThunar.App-0.6.1-full.nupkg` | 111,999,600 | `7bd30132d37dcc73813a45cd21a87a94972289c54c51ae28828f9415aae13270` |

## Limits

Accent changes made in Windows Settings while WinThunar is open are handled but were not exercised
by an automated check. Light-mode System colors was checked by the automated checks but not
inspected on screen. Mica falls back to a solid color when Windows transparency effects are off.
Installed upgrade to 0.6.1, slow-network stability, notification delivery and manual
screen-reader, contrast and scaling review remain unverified.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy all-users migration remains refused.
