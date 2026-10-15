# Security policy

## Supported versions

Only the latest release receives fixes. Installed copies update themselves from GitHub Releases;
portable copies must be replaced by hand.

## Reporting a vulnerability

Please **do not open a public issue** for a security problem. Use GitHub's private reporting
instead: go to the repository's **Security** tab and choose **Report a vulnerability**.

Useful details: the WinThunar version, how it was installed, steps to reproduce, and what an
attacker could gain. Reports about path traversal, symbolic-link or junction handling, unsafe
plugin or custom-action execution, and the shell-registration registry changes are in scope.

You can expect an acknowledgement within a few days. Fixes ship in a normal release, and the
release notes credit the reporter unless they prefer to stay anonymous.

## Scope notes

- Plugins and custom actions run commands you configure; installing a third-party plugin manifest
  is a trust decision, the same as installing any program.
- WinThunar's shell-integration settings write only to the current user's registry hive
  (`HKCU\Software\Classes`) and are reversible from Preferences or with `--unregister-shell`.
