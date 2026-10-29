# WinThunar 0.6.1

Prepared for Windows 11 x64. Project, assembly/file, manifest and release-script versions are
aligned at 0.6.1 (four-part version 0.6.1.0). Publication details are added once the release is
public.

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

The release source passes the service tests and the desktop smoke suite, whose theme checks now
cover all four choices. For each theme, in both an existing and a new window, they check the
backdrop, the page and file-pane surfaces, and that file rows draw their selection with the
theme's shared brush; System colors must show Mica, translucent panes and the current Windows
accent. On a dark-mode Windows 11 desktop, window captures show the Mica backdrop, the translucent
panes, the selected tab joining the pane and an accent-tinted selected row.

## Limits

Accent changes made in Windows Settings while WinThunar is open are handled but were not exercised
by an automated check. Light-mode System colors was checked by the automated checks but not
inspected on screen. Mica falls back to a solid color when Windows transparency effects are off.
Installed upgrade to 0.6.1, slow-network stability, notification delivery and manual
screen-reader, contrast and scaling review remain unverified.

An automatic app update does not replace an older cached MSI. Use the latest MSI for installer
safeguards. Legacy all-users migration remains refused.
