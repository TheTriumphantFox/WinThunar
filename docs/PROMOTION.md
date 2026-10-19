# WinThunar launch kit

Prepared 19 October 2026. These are reviewable drafts; no community posts, emails,
directory submissions, or advertising purchases have been made.

## Positioning

**One line:** A classic file manager experience for Windows 11.

**Short description:** WinThunar is a free, open-source Windows 11 file manager inspired
by Thunar, with tabs, split panes, bulk rename previews, and an integrated PowerShell panel.

**Independence:** WinThunar is an independent, clean-room implementation under the MIT
License. It is not an official Thunar or Xfce project. Use WinThunar's own icon and app
captures; do not reuse Thunar/Xfce artwork or imply endorsement.

## Links and assets

- Project: https://github.com/TheTriumphantFox/WinThunar
- Releases: https://github.com/TheTriumphantFox/WinThunar/releases/latest
- Windows installer: https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win.msi
- Portable ZIP: https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win-Portable.zip
- Feedback: https://github.com/TheTriumphantFox/WinThunar/issues
- Main screenshot: [split panes](media/winthunar-split-panes.png)
- Additional screenshots: [overview](media/winthunar-overview.png), [bulk rename preview](media/winthunar-bulk-rename.png), [PowerShell panel](media/winthunar-powershell.png)
- Demo: [MP4](media/winthunar-demo.mp4), [GIF](media/winthunar-demo.gif)
- Social image: [1280 × 640 project card](media/winthunar-social.png)
- Provenance: [capture report](media/capture-result.json)

All app images come from the v0.5.0 source, rendered with synthetic files in an isolated
capture build. The demo is an edited sequence of real rendered app states with captions,
not a continuous recording or a performance benchmark. The dialog capture shows the actual
bulk-rename preview. The capture harness then applies that plan and calls the released
transfer command on the sample files. No personal documents are included.

## Community introduction

**Title:** WinThunar: a free, open-source file manager inspired by Thunar, built for Windows 11

I'm sharing WinThunar, an independent Windows 11 file manager inspired by Thunar's classic
layout. It's free and open source under the MIT License.

It has tabs, split-pane browsing, a bulk-rename preview, and an integrated PowerShell panel.
The short demo shows renaming a group of sample files and organizing them between two folders.

Windows 11 x64 builds are available as an installer or a portable ZIP. Installed copies can
update from GitHub Releases; portable copies are updated manually.

It's still being developed, and I'd appreciate feedback from people who use alternative file
managers: **what would stop you using this for everyday file management?** Installation
friction, confusing navigation, and reproducible bugs are especially useful to hear about.

Project and downloads: https://github.com/TheTriumphantFox/WinThunar

WinThunar is a clean-room implementation, not an official Thunar or Xfce port.

**Before posting:** Read that community's current rules. Identify your relationship to the
project accurately. Where AI disclosure is required, include the true development history:
Codex prepared this launch copy and promotional assets; the maintainer should confirm which
AI tools were used for application code before describing that work. Do not post an
incomplete disclosure or invent a personal origin story.

## Short social post

WinThunar brings a classic file manager layout to Windows 11: tabs, split panes, bulk rename
previews, and a PowerShell panel. Free and open source (MIT).

Try the installer or portable build and tell us what you'd improve:
https://github.com/TheTriumphantFox/WinThunar

Attach the demo or the social image. Use your own voice when introducing why you built it.

## Show HN

**Title:** Show HN: WinThunar – a Thunar-inspired file manager for Windows 11

**Submission URL:** https://github.com/TheTriumphantFox/WinThunar

**First comment draft:**

I'm sharing WinThunar, a free, MIT-licensed file manager for Windows 11. It's an independent
implementation inspired by Thunar's classic layout, built with C# and WinUI 3.

The README has a short demo and direct installer/portable downloads. Features include tabs,
split panes, bulk rename previews, and an integrated PowerShell panel. The build targets x64
and doesn't require a separate .NET installation.

I'd particularly like feedback on the file-management workflow and installation experience.
It remains under development: Replace and directory Merge are outside transfer undo, and
Windows-handled operations aren't included in its Undo history.

**Before submitting:** The maintainer should add a truthful sentence explaining the motivation
for building it and be available to answer questions. Follow the [Show HN guidelines](https://news.ycombinator.com/showhn.html).
Do not solicit upvotes or describe routine patch releases as a new launch.

## AlternativeTo listing

**Name:** WinThunar

**Website:** https://github.com/TheTriumphantFox/WinThunar

**Platform:** Windows (Windows 11 x64)

**License / pricing:** Open source, MIT / Free

**Category:** File manager

**Suggested tags:** file-manager, windows, tabs, dual-pane, bulk-rename, open-source

**Description:**

WinThunar is an independent, open-source file manager for Windows 11 inspired by the classic
Thunar experience. It combines menus, a location bar, and a navigation side pane with tabs,
split-pane browsing, multiple folder views, bookmarks, recursive search, and bulk rename
with a preview. An integrated PowerShell panel follows the active folder. Installer and
portable x64 builds are available from GitHub Releases. The project uses C# and WinUI 3
and is released under the MIT License.

**Suggested alternatives:** File Explorer; Thunar, with the distinction that WinThunar runs
on Windows. Submit only appropriate relationships and let the directory review them.

Submit through [AlternativeTo's application process](https://alternativeto.net/faq/).
Do not submit fake reviews or endorsements.

## Press or creator pitch

**Subject:** WinThunar: a free Thunar-inspired file manager for Windows 11

Hello,

I'd like to suggest WinThunar for your Windows utility coverage. It's an independent,
MIT-licensed Windows 11 file manager inspired by Thunar's classic layout, built with WinUI 3.

A useful demonstration is its bulk-rename preview followed by organizing files between two
panes. It also offers tabs and an integrated PowerShell command panel. Installer and portable
x64 downloads are available without an account or a separate .NET installation.

The project page includes screenshots and a 25-second demo:
https://github.com/TheTriumphantFox/WinThunar

I'd be glad to answer questions or provide more details about the project.

Thank you.

**Sending notes:** Personalize the opening for the recipient's actual coverage. Add the
maintainer's preferred signature. A possible first outlet is [Neowin's news-tip/contact route](https://www.neowin.net/contact/).
No coverage or endorsement is implied.

## Launch order and measurement

1. Review the README, media, public release downloads, and known limitations. Publish the
   documentation/media changes to GitHub before sharing links to them.
2. Submit the AlternativeTo listing and make one appropriate community introduction.
   Check current community rules and obtain moderator permission where required.
3. Respond to useful reports and address installation or file-operation problems.
4. Make a Show HN submission when the maintainer can answer questions. Send individual
   pitches to relevant Windows utility writers or creators.
5. Consider WinGet as a separate packaging task. Microsoft Store submission also needs
   its own packaging, signing, and policy review; neither is claimed to be available here.

The [r/Windows11 moderator discussion from September 2026](https://www.reddit.com/r/Windows11/comments/1w4fj7x/looking_for_feedback_regarding_handling_of/)
proposed a minimum 30-day public history and AI disclosure among its conditions. That
discussion is not permission to post. Verify current rules and the earliest public project
date before requesting approval; the latest release date alone does not establish age.

Use this small manual log. Release download counts are downloads, not unique or active users.
Record a baseline and distinguish human-facing installer/ZIP assets from update packages.

| Date | Channel / post URL | Installer downloads | Portable downloads | Detailed feedback reports | Follow-up |
| --- | --- | --- | --- | --- | --- |
| | Baseline before launch | | | | |
| | | | | | |

Initial target: 20 people trying the app and five useful feedback reports. Confirm people
tried it through voluntary feedback rather than equating download counts with users.
Keep the first campaign unpaid while learning which message and audience respond.

## Rebuilding the captures

From the repository root, run:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\promotion\New-PromotionalCaptures.ps1 -SourceRef v0.5.0
```

The script archives the specified release into a new temporary directory, instruments only
that disposable copy, redirects app storage there, disables update checks and shell repair,
and captures the released UI with sample files. It never changes the working application's
source or uses the release artifacts directory. Its dialog callback captures the real rename
preview and then returns acceptance to the actual rename routine on the synthetic fixture.
It leaves the temporary build and report available for inspection.

The capture callback is tailored to the 0.5.0 source structure. Review it before using a
different version. The media composer requires Pillow and an FFmpeg executable; see
`scripts/promotion/Compose-PromotionalMedia.py --help`.
