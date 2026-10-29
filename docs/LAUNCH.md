# First tester launch

Prepared 19 October 2026 for **WinThunar 0.5.0, Windows 11 x64**. The initial target is
20 voluntary testers and five detailed feedback reports. Download counts do not establish
that a person tried the application or continued using it.

## Current status

The initial campaign below targeted 0.5.0. The current release is [WinThunar
0.6.0](https://github.com/TheTriumphantFox/WinThunar/releases/tag/v0.6.0), published 29 October
2026. It closes the roadmap's P0 stability work, which was the hold on Show HN and press
pitches. As of 29 October no outside tester reports have arrived; see the [29 October
counts](#baseline-and-results). A second round is prepared but not posted: a [0.6.0 follow-up
post](PROMOTION.md#follow-up-post-for-060), a [Linux and Xfce
post](PROMOTION.md#linux-and-xfce-communities), media recaptured from 0.5.15 (still accurate
for 0.6.0), a README rewritten for first-time visitors, and the [distribution
channels](DISTRIBUTION.md).

| Item | Status |
| --- | --- |
| Project introduction, screenshots, and 25-second demo | Published in PR #10. |
| Documentation audit | Published in PR #11. |
| GitHub tester-feedback form | Published in PR #11 and linked from the README. |
| AlternativeTo listing | [Approved and public](https://alternativeto.net/software/winthunar/), confirmed by the maintainer on 26 October 2026 (submitted 19 October). |
| First community introduction | [Published in r/SideProject](https://www.reddit.com/r/SideProject/comments/1x9uvud/winthunar_a_free_thunarinspired_file_manager_for/) by u/TheTriumphantFox on 19 October 2026. |
| Windows app introduction | [Published in r/windowsapps](https://www.reddit.com/r/windowsapps/comments/1x9w8ux/winthunar_a_free_windows_11_file_manager_with/) by u/TheTriumphantFox with Developer flair on 19 October 2026; public post verified. |
| r/Windows11 | Excluded: current promotion rules say new applicants are not being accepted. |
| 0.6.0 follow-up post (r/windowsapps) | Drafted 26 October 2026 for 0.5.15 and updated for 0.6.0 on 29 October; not posted. |
| Linux and Xfce communities | Drafted 26 October 2026; not posted. Ask moderators first. |
| WinGet | Install, match, launch and uninstall passed in the lab VM on 26 October 2026 after fixing the product code; not submitted. |
| Scoop | In-repository bucket on `main`; install, launch and uninstall passed in the lab VM on 26 October 2026. Not yet in Scoop Extras. |
| Show HN and press pitches | No longer held: the P0 stability work shipped in 0.6.0 on 29 October 2026. Not submitted. |
| Paid promotion | Not part of this launch. |

Update these statuses only after observing a successful submission or a public post URL.
AlternativeTo submissions can remain pending review and are not public listings until approved.
The earlier PR #12 record described the AlternativeTo copy as a draft with browser sign-in pending;
the maintainer-confirmed submission status above supersedes that preparation state.

## A short workflow for testers

1. Download the [MSI installer](https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win.msi)
   or [portable ZIP](https://github.com/TheTriumphantFox/WinThunar/releases/latest/download/WinThunar.App-win-Portable.zip).
2. Use sample files in two test folders. Open a tab and toggle split view with `F3`.
3. Select several sample documents, press `F2`, and inspect the bulk-rename preview before
   applying a rename. Move or copy those samples into the second folder.
4. Tell us what was confusing or failed using the
   [tester-feedback form](https://github.com/TheTriumphantFox/WinThunar/issues/new?template=tester-feedback.yml).

Use only released features in the first announcement. Transfer Undo does not include
Replace or directory Merge. Reports should remove personal paths and filenames from logs
and screenshots before posting them publicly.

## AlternativeTo submission fields

| Field | Value |
| --- | --- |
| Name | WinThunar |
| Website | https://github.com/TheTriumphantFox/WinThunar |
| Platform | Windows; requires Windows 11 x64 |
| Pricing | Free |
| Open source | Yes; MIT |
| Source code | https://github.com/TheTriumphantFox/WinThunar |
| Category | File manager |
| Suggested tags | file-manager, windows, tabs, dual-pane, bulk-rename, open-source |
| Screenshot | `docs/media/winthunar-split-panes.png` |
| Original icon | `Assets/AppIcon.ico` |

**Short description:** A free, open-source Windows 11 file manager inspired by Thunar, with
tabs, split panes, and bulk rename previews.

**Full description:**

WinThunar is an independent file manager for Windows 11 inspired by Thunar's classic layout.
It combines menus, a location bar, and a navigation side pane with tabs, split-pane browsing,
bookmarks, multiple folder layouts, recursive filename search, and bulk rename with a preview.
An integrated PowerShell command panel follows the active folder. Windows 11 x64 installer
and portable builds are available from GitHub Releases; a separate .NET installation is not
required. Installed copies support automatic updates. WinThunar is released under the MIT
License and is not an official Thunar or Xfce project.

Suggested alternatives: File Explorer and Thunar where the platform distinction is clear.
The [submission FAQ](https://alternativeto.net/faq/#add-a-new-application) requires a verified
email address. Standard review has no promised completion date. No paid review is authorized
by this launch plan.

## First community introduction: r/SideProject

Posting account: **u/TheTriumphantFox**, confirmed by the maintainer.

The [official rules endpoint](https://www.reddit.com/r/SideProject/about/rules.json) returned
no subreddit-specific numbered rules when checked on 19 October 2026. That does not guarantee
posting permission: inspect live pinned guidance, account restrictions, and the submission
screen before posting. Post once and participate in replies; do not solicit votes.

**Title:** WinThunar: a free Thunar-inspired file manager for Windows 11 — looking for first testers

**Body (published text):**

I'm sharing WinThunar, a free, open-source file manager for Windows 11. It uses a classic
menu-and-sidebar layout inspired by Thunar, with tabs, split panes, bulk rename previews,
and an integrated PowerShell command panel.

The project page has a 25-second demo showing a group of sample files being renamed and
organized between two folders:

https://github.com/TheTriumphantFox/WinThunar

The current release is 0.5.0 for Windows 11 x64, available as an installer or a portable ZIP.
It's an independent MIT-licensed implementation, not an official Thunar/Xfce port.

I'm looking for a small first group of testers. If you try it, I'd particularly appreciate
feedback on installation, navigating between folders, and anything confusing in the
bulk-rename or split-pane workflow. Replace and directory Merge are outside transfer Undo.

What would stop you using this for everyday file management?

Disclosure: Codex helped prepare this announcement and the demo.

**Posting note:** The maintainer should confirm the account identity and add an accurate
development/AI disclosure if required by live community guidance. Codex prepared this launch
copy and the promotional assets; the rest of the development history has not been supplied
for this announcement. Do not describe unverified history or claim to have personally made
work that the posting account did not make.

Attach the existing demo or screenshot if the posting form supports it. The
[feedback form](https://github.com/TheTriumphantFox/WinThunar/issues/new?template=tester-feedback.yml)
can collect reproducible reports after the first replies.

## Baseline and results

GitHub release counters checked on 19 October 2026 before the first external submission:

| Asset / signal | Baseline |
| --- | ---: |
| MSI downloads | 0 |
| Portable ZIP downloads | 1 |
| Setup EXE downloads | 2 |
| Repository stars | 0 |
| External post URLs verified in this launch | 0 |

These counters include any downloads by the maintainer or automated tools. Update packages
and feed requests are excluded from the human-facing download totals. Store a fresh baseline
if the actual public submission happens later.

Counters summed across every release on 26 October 2026, a week after the first posts. Release
verification downloads each public binary, so most or all of the increase may be the
maintainer's own checks.

| Asset / signal | 26 October 2026 | Change since 19 October |
| --- | ---: | ---: |
| MSI downloads | 2 | +2 |
| Portable ZIP downloads | 8 | +7 |
| Setup EXE downloads | 3 | +1 |
| Repository stars | 1 | +1 |
| Outside issues or tester reports | 0 | 0 |

Counters summed across every release on 29 October 2026, the day 0.6.0 was published. The WinGet
and Scoop install tests on 26 October downloaded the MSI and portable ZIP in the lab VM, so part
of the increase is the maintainer's own testing; the source of the Setup EXE downloads is unknown.

| Asset / signal | 29 October 2026 | Change since 26 October |
| --- | ---: | ---: |
| MSI downloads | 8 | +6 |
| Portable ZIP downloads | 9 | +1 |
| Setup EXE downloads | 17 | +14 |
| Repository stars | 1 | 0 |
| Outside issues or tester reports | 0 | 0 |

| Date | Channel and public URL | Submission/review status | Downloads since baseline | Voluntary tester reports | Useful follow-up |
| --- | --- | --- | --- | --- | --- |
| 19 October 2026 | [AlternativeTo](https://alternativeto.net/software/winthunar/) | Approved; confirmed by maintainer on 26 October 2026 | | | Keep its screenshot and description current with each announcement. |
| 19 October 2026 | [r/SideProject](https://www.reddit.com/r/SideProject/comments/1x9uvud/winthunar_a_free_thunarinspired_file_manager_for/) | Published; public post verified | | | Respond to substantive feedback. |
| 19 October 2026 | [r/windowsapps](https://www.reddit.com/r/windowsapps/comments/1x9w8ux/winthunar_a_free_windows_11_file_manager_with/) | Published with Developer flair; author, text, and public visibility verified | | | Respond to substantive feedback; respect the one promotional post per week limit. |
