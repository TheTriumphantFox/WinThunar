# Accessibility status

Reviewed against the source checkout on 21 October 2026. This is a source and smoke-check
status document, not a claim of full screen-reader compatibility or accessibility conformance.
The latest published application is 0.5.3; the support below is merged into GitHub `main`
and included in that release, subject to the verification limits here.
See the [release/source status](ROADMAP.md#release-and-source-status).

## Support in 0.5.3

- `F6` / `Shift+F6` cycles between folder tabs, the location control, the visible side pane,
  the primary file view, the right pane if open, and the command input if visible.
- `Ctrl+Tab` / `Ctrl+Shift+Tab` switches folder tabs.
- Moving keyboard focus into a file pane makes it the active target for file commands.
- Focusable detail-column dividers accept Left/Right to resize by ten units.
- Location, search, file lists, preview, transfer progress, and command-panel controls have
  explicit accessible names. File containers expose filename, type, size where available,
  and optional Git status text. Recycled containers have their old name cleared.
- Git status is text as well as a visual badge; status tooltips do not depend on color alone.

Release 0.5.3 follows Windows light/dark settings and uses WinUI system resources for the
browser chrome, command panel, preview path, and drop highlight. Light/dark desktop renders
were inspected. Full high-contrast support must be verified rather than inferred from resources.

## Automated desktop smoke coverage

The source checkout includes the following command. The harness is committed to GitHub
`main` and excluded from normal application packages:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\Test-DesktopSmoke.ps1
```

The script builds a separate test executable with `RoadmapSmokeTest=true`, a distinct
single-instance key, and a generated temporary fixture. Its page path bypasses ordinary
session restoration and transfer recovery and does not save a normal session. Automatic
update checks and shell-registration repair are disabled for the smoke build. Windows
jump-list and notification registration are still attempted, so it is not a sandbox for
every possible Windows integration side effect.

It checks a one-file detailed listing, accessible location/list names, the realized file
container's name, selected F6/Shift+F6 transitions, Git-status text, and visibility of the
three primary layouts. It renders PNGs and writes `result.json`. Jump-list registration
errors fail the harness; notification-registration unavailability is recorded as a status
message, not treated as proof of a working notification.

Those assertions do not run a screen reader, exercise every focus order, or prove that
Windows displayed a notification. File-service tests are separate; see
[file-operation verification](FILE-OPERATIONS.md#verification).

The expanded 0.5.2 harness also checks Windows-theme inheritance and light/dark renders with
the command panel, combined search filters and invalid-filter feedback, queued replacement
registration in window history, Undo/Redo content restoration, and retained-data expiry.
Its file-operation journal is redirected into its generated fixture directory.

## Verification

On 21 October, the expanded desktop harness passed its assertions and the light/dark renders
were inspected. Notification registration still returned the module-loading diagnostic below.
The final 0.5.3 service suite completed with **125 passed, one skipped, zero failed**.
See [release notes](RELEASE-0.5.3.md) for current test scope and remaining storage checks.

The previous baseline on 19 October is retained for comparison:

The documented service test command completed with **85 passed, one skipped, zero failed**.
The skip was the relative-symbolic-link case: this account lacked Windows permission to
create that link. The desktop smoke command also passed its assertions, including the three
rendered layouts, accessible names, selected F6 transitions, and jump-list registration.

Notification registration was unavailable. Its diagnostic included a module-loading error
for `Microsoft.WindowsAppRuntime.Insights.Resource.dll`. The smoke harness records that
condition without failing; notification delivery and activation were not verified in this
run. A passing smoke result must not be presented as a complete notification or accessibility
check.

## Manual checks still required

- Narrator and another Windows screen reader: reading order, announcements, multi-selection,
  menus, tabs, search results, transfer state, and modal dialogs.
- Keyboard-only use of both panes, tree navigation, column dividers, custom actions, and all
  dialogs, including focus restoration after opening or closing them.
- Windows contrast themes and light/dark settings, including fixed-color areas and focus cues.
- Display/text scaling, long paths, narrow windows, localization, and clipping.
- Touch/pointer drag/drop and alternate keyboard shortcuts without disrupting text entry.
- Taskbar tasks, background-transfer notifications, and notification activation across
  installed/portable launches and Windows notification settings.

Keep these checks open until observed on the relevant Windows configuration. Update this
page and the roadmap when the support is released or the manual checks are completed.

## Additional unreleased desktop coverage

The current source smoke harness also checks the recovery warning/dialog and accessible,
selectable recovery details; preserved corrupt journals; text-content search; a read-only text
preview; visible filename width with the preview open; and This PC search scope/return behavior.
This PC tests use generated folders as synthetic drive roots and do not recursively search the
user's drives. The updated assertions pass and rendered recovery/preview views were inspected.
See [reliability evidence](RELIABILITY-MILESTONE.md). These checks do not close the manual
screen-reader, contrast, scaling, Shell, notification, or pointer-workflow checklist above.

The document-preview extension checks native two-page PDF rendering, accessible Previous/Next
names, a polite page-number live region, first/final-page button states, malformed/oversized
input, source-handle release, and selection cancellation. Rendered PDF controls were inspected
at this machine's 125% display scale. The preview is an image of the page; it does not expose
extracted PDF body text to a screen reader. The native index query is also checked, with an
explicit coverage warning and a separate read-only metadata probe; this does not establish
document-filter coverage or screen-reader result announcements.
