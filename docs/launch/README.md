# WinThunar launch kit

Everything needed to announce WinThunar, in the order to do it. Nothing here has been posted
anywhere; each step is yours to run.

## Before you announce (repository)

- [ ] Merge this branch, then check that the `CI` workflow passes on `main`.
- [ ] **Screenshots and a demo GIF.** Capture them on Windows using the list in
      [screenshots.md](screenshots.md), save them in `docs/images/`, and uncomment the TODO block in
      the README. A post without a picture loses most readers.
- [ ] **GitHub repo settings**
  - Description: `The Thunar file manager experience, built natively for Windows 11 (WinUI 3, .NET 10)`
  - Website: the GitHub Pages URL (see below)
  - Topics: `file-manager`, `thunar`, `windows-11`, `winui3`, `dotnet`, `csharp`, `windows`,
    `open-source`, `tabs`, `explorer-alternative`
  - Social preview: upload `docs/site/assets/social-preview.png` (Settings > General > Social preview)
  - Enable Discussions, and create the labels `good first issue`, `help wanted`, `plugin`
- [ ] **Publish the landing page.** Settings > Pages > Source: *GitHub Actions*. The `pages` workflow
      deploys `docs/site/`. The site will be at `https://thetriumphantfox.github.io/WinThunar/`.
- [ ] **Release notes.** Release v0.5.0 currently has an empty description. Paste the 0.5.0 section of
      [CHANGELOG.md](../../CHANGELOG.md) into it.
- [ ] **Fresh-machine test.** Install the MSI on a clean Windows 11 account (or a VM) and click through
      the first-run experience yourself. Fix anything rough before strangers see it.
- [ ] **Windows SmartScreen.** Unsigned installers show a warning. Say so in your posts (see the FAQ
      answers in [posts.md](posts.md)) and consider free code signing later (SignPath Foundation
      offers it to open-source projects).

## Distribution (so people can find and install it)

- [ ] **winget.** Submit the manifests in [`packaging/winget`](../../packaging/winget) to
      `microsoft/winget-pkgs` (the README there has the steps). `winget install` is the most
      credible install path for Windows power users.
- [ ] **Scoop.** Put [`packaging/scoop/winthunar.json`](../../packaging/scoop/winthunar.json) in a
      bucket of your own, or propose it to the `extras` bucket.
- [ ] **Directory listings** (free, long-tail search traffic): AlternativeTo, Slant, SourceForge
      mirror, Softpedia, MajorGeeks, Chocolatey (community repository). Reuse the text in
      [posts.md](posts.md#directory-listing-text).

## Announce (suggested order and timing)

1. **Soft launch (day 0).** Post to one friendly, small place first (a Discord, a friend group, or
   r/opensource) and fix whatever they hit.
2. **Show HN and Reddit (day 1-3).** Post on a weekday morning US Eastern time. Use the drafts in
   [posts.md](posts.md). Be present for the first few hours to answer comments.
   Space Reddit posts out across days; do not paste the same text everywhere.
3. **Write-up (week 1).** Publish one technical article (outlines in
   [posts.md](posts.md#technical-article-outlines)) on Dev.to or your own blog, then link it from the
   repo README.
4. **Product Hunt (week 2-3).** Only once you have screenshots, a GIF, and a few early users who can
   leave honest comments.
5. **Lists and newsletters (ongoing).** Awesome-list pull requests and newsletter pitches
   (texts in [posts.md](posts.md#awesome-list-entries)).

## After launch

- Reply to every issue and comment within a day or two for the first month.
- Turn repeat questions into README or docs sections.
- Tag small, well-described issues `good first issue`.
- Ship small releases often and post short changelog notes; the in-app updater makes this cheap.
- Track what works: GitHub traffic (Insights > Traffic), release download counts, and stars by
  referrer.

## Rules of the road

- Read each community's self-promotion rules before posting, and follow them.
- Say you are the author. Never ask friends to upvote or use sock accounts; communities detect it
  and it backfires.
- Be honest about limits: Windows 11 only, unsigned installer, features that are still on the
  [roadmap](../ROADMAP.md).
- Do not claim affiliation with Thunar or the Xfce project. The name is a tribute, and the project is
  clean-room.
