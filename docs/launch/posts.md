# Post drafts

Edit these into your own voice before posting; replace anything in `[brackets]`. Links:

- Repo: https://github.com/TheTriumphantFox/WinThunar
- Latest release: https://github.com/TheTriumphantFox/WinThunar/releases/latest
- Site: https://thetriumphantfox.github.io/WinThunar/ (after Pages is enabled)

## One-liners

- **Tagline:** The Thunar file manager experience, built natively for Windows 11.
- **Short:** WinThunar is a free, open-source, Windows-native file manager inspired by Thunar, with tabs, split panes, bulk rename, undo/redo, and plugins.
- **Elevator pitch:** If you miss Thunar's layout and habits on Windows, or find Explorer too limited, WinThunar gives you tabs, a split view, a transfer queue that will not destroy your files if something fails, previewed bulk rename, undo/redo, and a JSON plugin system, in a single native WinUI 3 app.

## Show HN

**Title:** Show HN: WinThunar, a Thunar-style file manager for Windows 11 (WinUI 3, MIT)

**Body (first comment):**

> I missed Thunar's layout and keyboard habits after moving a lot of work to Windows, so I built a
> clean-room, Windows-native version (no Thunar code; it's MIT). It's C#/.NET 10/WinUI 3.
>
> The part I spent the most time on is making file operations hard to break. Copies are staged at the
> destination and replacements roll back if anything fails, so an interrupted transfer can't destroy
> the file that was already there; there's a persistent journal to recover interrupted jobs after a
> crash; cross-volume copies keep NTFS alternate data streams and security metadata; and a ten-step
> undo/redo only reverses an operation if the destination still matches what the app expects.
>
> Other features: tabs and split panes, recursive search with live results, previewed bulk rename,
> a sequential transfer queue with progress/ETA, Thunar's drag-and-drop move/copy rules, an
> integrated PowerShell panel (F4), and JSON plugins/custom actions. It can optionally be registered
> as the handler for folders (HKCU only, fully reversible).
>
> Known limits: Windows 11 only, the installer isn't code-signed yet (so SmartScreen will warn), and
> directory junction transfer is deliberately blocked until I can make it link-safe.
>
> Would love feedback on the transfer/rollback design in particular, and on which Thunar behaviors
> you'd want next. Repo: https://github.com/TheTriumphantFox/WinThunar

## Reddit

Post each to one community per day, using that community's flair, and read its rules first. Say
that you are the author.

### r/opensource, r/windows11, r/software (general)

**Title:** I built an open-source, Thunar-style file manager for Windows 11 (tabs, split panes, bulk rename, undo/redo)

> Hi all, I'm the author. WinThunar is a free, MIT-licensed file manager for Windows 11 that recreates
> the Thunar experience (clean-room, no Thunar code). Highlights:
>
> - Tabs with separate histories and a split-pane view
> - Transfer queue with progress and ETA; copies are staged so a failure can't destroy the original
> - Previewed bulk rename and 10-step undo/redo
> - Recursive search with live results
> - Built-in PowerShell panel (F4)
> - JSON plugins and custom actions
> - Optional "Open in WinThunar" / default-folder handler, reversible, current user only
>
> Installer (auto-updates) and portable ZIP: https://github.com/TheTriumphantFox/WinThunar/releases/latest
>
> It's early, so bug reports are very welcome. Note the installer isn't code-signed yet, so
> SmartScreen will warn. [screenshot / GIF]

### r/xfce, r/linux, r/linuxmasterrace (Thunar fans)

**Title:** For anyone who misses Thunar at work: a Thunar-style file manager for Windows

> I use Thunar on Linux and got tired of Explorer on my Windows machines, so I wrote a Windows-native
> recreation (clean-room; not affiliated with Xfce). It keeps the layout, the "name (copy 1)"
> duplicate naming, the drag-and-drop rules (move on the same volume, copy across volumes, Ctrl/Shift
> to force), bulk rename, and the keyboard habits. [GIF]
>
> It's MIT and on GitHub: https://github.com/TheTriumphantFox/WinThunar. Tell me which Thunar
> behaviors I'm still missing.

### r/dotnet, r/csharp, r/WinUI (technical)

**Title:** What I learned building a file manager in WinUI 3 and .NET 10

> Writeup of a WinUI 3 file manager: staged copies with rollback, crash-recovery journal, junction
> safety, shell integration without touching HKLM, and Velopack for installer + delta updates from
> GitHub Releases. [link to article]. Repo: https://github.com/TheTriumphantFox/WinThunar

## Product Hunt

- **Name:** WinThunar
- **Tagline (60 chars max):** Thunar-style file manager for Windows 11
- **Description (260 chars max):** A free, open-source file manager for Windows 11 inspired by Thunar. Tabs, split panes, previewed bulk rename, undo/redo, a safe transfer queue, a built-in PowerShell panel, and JSON plugins. Installer or portable ZIP.
- **Topics:** Open Source, Developer Tools, Productivity, Windows
- **First comment (maker):** Say why you built it, what you are proudest of (safe file operations), what is
  unfinished (taskbar jump list, version-control overlays, accessibility review), and ask what to build next.

## Short social posts (Mastodon, Bluesky, X, LinkedIn)

> WinThunar 0.5: a free, open-source, Thunar-style file manager for Windows 11. Tabs, split panes,
> bulk rename, undo/redo, plugins, and an installer that updates itself. Clean-room, MIT.
> https://github.com/TheTriumphantFox/WinThunar #opensource #Windows11 #dotnet #WinUI

> Explorer too limited, Thunar missing on Windows? WinThunar: tabs, split view, safe transfer
> queue, bulk rename, PowerShell panel (F4). Free & open source. [GIF]
> https://github.com/TheTriumphantFox/WinThunar

> Building a file manager taught me: never overwrite the destination until the new copy is complete.
> WinThunar stages copies and rolls back failed replacements. Notes: [article link]

## Technical article outlines

Pick one; each is a standalone post for Dev.to, Hashnode, or your blog.

1. **"Making file copies that can't destroy your data."** Why naive copy-over-existing loses files on
   failure; staging next to the destination; atomic replace with rollback; the recovery journal;
   preserving NTFS alternate data streams and security descriptors; what is deliberately not undoable
   (Replace and Merge) and why.
2. **"Thunar's drag-and-drop rules on Windows."** Same-volume vs cross-volume detection, modifier keys,
   folder-row drop targets in WinUI 3, and the pitfalls of the WinUI drag-drop APIs.
3. **"Shipping a WinUI 3 app outside the Store."** Self-contained unpackaged builds, Velopack MSI with
   per-user/per-machine choice, delta updates from GitHub Releases, and a reversible `HKCU` shell
   registration. (The README's *Windows release* section is a good source.)
4. **"Plugins as JSON."** Designing a manifest format, selection-aware actions with file patterns, and
   running commands from argument lists instead of a shell.

## Awesome-list entries

Search for each list's contribution rules first, then send a one-line pull request in its format.

- Awesome-Windows (Windows software): `[WinThunar](https://github.com/TheTriumphantFox/WinThunar) - Thunar-style file manager for Windows 11 with tabs, split panes, bulk rename, undo/redo, and plugins.`
- Awesome .NET / awesome-dotnet (desktop apps): same line, under desktop applications.
- Awesome WinUI / awesome-WinUI3: `[WinThunar](...) - Open-source WinUI 3 file manager.`
- Awesome file managers lists, if one exists, under "Windows".

## Newsletter pitch

**Subject:** WinThunar: open-source Thunar-style file manager for Windows 11

> Hi [name], I'm [your name], the author of WinThunar, a free, MIT-licensed file manager for Windows 11
> inspired by Thunar. It has tabs, split panes, previewed bulk rename, ten-step undo/redo, and a
> transfer queue designed so failed copies can't destroy existing files. It ships as a self-updating
> installer or a portable ZIP. If it fits [newsletter], the repo is
> https://github.com/TheTriumphantFox/WinThunar and I can send screenshots. Thanks for considering it.

Good targets: This Week in .NET, Console.dev, Awesome Windows-style roundups, the r/opensource weekly
threads, and Windows power-user blogs.

## Directory listing text

- **Name:** WinThunar
- **Category:** File Manager / Explorer alternative
- **Platform:** Windows 11 (x64)
- **License:** Open source (MIT), free
- **Alternatives to list it against:** Windows File Explorer, Files, Directory Opus, Total Commander,
  Double Commander, Explorer++, Thunar (Linux)
- **Short description (about 160 chars):** Free, open-source, Thunar-style file manager for Windows 11 with tabs, split panes, bulk rename, undo/redo, and plugins.
- **Long description:** WinThunar recreates the classic Thunar file manager experience as a native
  Windows 11 app built with WinUI 3 and .NET 10. It offers tabs with independent history, split-pane
  browsing, a transfer queue with progress and cancellation, rollback-safe copies, ten-step
  undo/redo, recursive search, previewed bulk rename, an integrated PowerShell panel, and a JSON
  plugin system for custom actions, archives, Git commands, and media tags. It is clean-room, MIT
  licensed, and available as a self-updating installer or a portable ZIP.
- **Tags:** file manager, explorer alternative, tabs, dual pane, open source, Windows 11

## Answers to questions you will get

- **"Why does Windows SmartScreen warn about the installer?"** The installer isn't code-signed yet.
  Signing certificates cost money; free signing for open-source projects (such as SignPath
  Foundation) is on the to-do list. GitHub shows a SHA-256 digest next to each release file so you can verify your
  download, and the full source is on GitHub.
- **"Why not just use Files / Explorer++ / Directory Opus?"** Those are good tools. WinThunar is for people
  who specifically want Thunar's layout and behavior, with an emphasis on safe file operations.
- **"Does it support Windows 10?"** Windows 11 only for now.
- **"Is it affiliated with Thunar or Xfce?"** No. It is an independent, clean-room project; the name
  is a tribute.
- **"Can it replace Explorer?"** Optionally, for folders and drives, via Preferences. Win+E and apps
  that call `SHOpenFolderAndSelectItems` still open Explorer, and it is fully reversible.
- **"ARM64?"** [Answer once you decide; the project has an ARM64 publish profile but releases are x64.]
