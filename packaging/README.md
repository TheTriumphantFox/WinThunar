# Packaging manifests

Drafts for getting WinThunar into Windows package managers. They were written without access to a
Windows machine, so **validate each one on Windows before submitting it.**

## winget

Files in [`winget/`](winget) describe version 0.5.0 using the `WinThunar.App-win.msi` release asset,
with the SHA-256 taken from the release's published digest. Still to do:

1. Install the tools: `winget install Microsoft.WingetCreate`.
2. Refresh and complete the manifest from the live release. This also fills in the MSI `ProductCode`,
   which could not be read here:

   ```powershell
   wingetcreate update TheTriumphantFox.WinThunar --version 0.5.0 `
     --urls https://github.com/TheTriumphantFox/WinThunar/releases/download/v0.5.0/WinThunar.App-win.msi
   ```

   (For a first submission you can instead run `wingetcreate new <msi url>` and compare with these files.)
3. Validate and test locally:

   ```powershell
   winget validate --manifest .\winget
   winget settings --enable LocalManifestFiles
   winget install --manifest .\winget
   ```

4. Submit a pull request to [`microsoft/winget-pkgs`](https://github.com/microsoft/winget-pkgs) (use
   `wingetcreate submit`). The first review can take a few days; later versions can be automated with
   the `vedantmgoyal9/winget-releaser` GitHub Action or `wingetcreate update --submit`.

Notes: the MSI is per-user and per-machine selectable, so `Scope: user` is the default declared here;
confirm what a silent install does and adjust. An unsigned MSI is accepted by winget, but SmartScreen
behavior should be tested.

## Scoop

[`scoop/winthunar.json`](scoop/winthunar.json) uses the Velopack portable ZIP. The folder layout inside
that ZIP was not inspected, so the `bin` and `shortcuts` paths (`current\WinThunar.exe`) are a guess.
Check with:

```powershell
scoop install .\packaging\scoop\winthunar.json
winthunar
```

Fix the paths if the executable is elsewhere, then either create your own bucket
(`scoop bucket add` from a repo containing this file under `bucket/`) or propose it to the `extras`
bucket following its contribution guide.

## Chocolatey (optional)

Community-repository packages need a `.nuspec` and a `chocolateyinstall.ps1`, and go through
moderation. The MSI makes this straightforward; see the
[Chocolatey package creation guide](https://docs.chocolatey.org/en-us/create/create-packages/).

## Keeping these in sync

Each release changes the version, URL, and hash in every manifest. `wingetcreate update` and Scoop's
`checkver`/`autoupdate` handle that for their ecosystems.
