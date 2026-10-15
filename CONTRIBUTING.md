# Contributing to WinThunar

Thanks for helping. WinThunar is a small project with one rule that matters more than any other:
**a file manager must never lose a user's files.** Changes to copy, move, replace, delete, or undo
code are reviewed with that in mind first and everything else second.

## Ways to help

- **Try it and report what breaks.** Use the [bug report form](https://github.com/TheTriumphantFox/WinThunar/issues/new?template=bug_report.yml).
  Reports from unusual setups (network shares, removable drives, OneDrive folders, very long paths,
  non-English Windows) are especially valuable.
- **Suggest Thunar behavior that is missing.** Say what Thunar does and what WinThunar does instead.
- **Write a plugin or custom action.** Plugins are JSON manifests, so no C# is required. See
  [`Plugins/`](Plugins) for the bundled examples.
- **Improve the docs.** The behavior notes in [`docs/`](docs) are the reference for how operations
  are meant to work.
- **Fix something.** Issues labeled `good first issue` are a reasonable start.

## Clean-room rule

WinThunar does not contain code copied from Thunar. Please do not paste Thunar (or other GPL)
source into a pull request, even a few lines. Describe the behavior you want and implement it
independently. Observing how Thunar behaves is fine; reading its source and re-typing it is not.

## Building and testing

You need Windows 11, the .NET 10 SDK, and Developer Mode enabled in Windows Settings.

```powershell
dotnet restore .\WinThunar.csproj -r win-x64
dotnet build .\WinThunar.csproj --configuration Debug -p:Platform=x64 --no-restore
dotnet run --project .\WinThunar.csproj --configuration Debug -p:Platform=x64
dotnet test .\tests\WinThunar.FileOps.Tests\WinThunar.FileOps.Tests.csproj --configuration Release
```

WinUI 3 cannot be built on Linux or macOS. If you do not have a Windows machine, open the pull
request anyway: CI builds and tests every pull request on a Windows runner.

## Pull requests

1. Open an issue first for anything large, so you do not build something that will not be merged.
2. Keep each pull request to one change. Match the surrounding code's naming and style.
3. Add or update tests in `tests/WinThunar.FileOps.Tests` for file-operation behavior. The tests use
   generated disposable directory trees, never real user folders.
4. For file operations, show that failure and cancellation leave the original data intact.
5. Update the relevant page in `docs/` and the README feature list when behavior changes.

## Licensing

By contributing you agree that your work is released under the [MIT License](LICENSE).
