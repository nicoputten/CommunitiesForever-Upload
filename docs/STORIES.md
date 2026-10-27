# Backlog

Open stories only, in build order. Done stories move to `docs/STORIES-COMPLETED.md`.
Each story: criteria are testable. "Manual" items are steps only Nico can do; they go in the PR.

Build order: UP-01, UP-02, UP-03, UP-04, UP-05, UP-06, UP-07, UP-08, UP-09, UP-10. All of it is v1 (decided with Nico).
The site side is built in the web repo: WEB-33 (uploader API), WEB-34 (uploader page), WEB-35 (installer hosting and update feed). UP-04 and later can build against a fake server before those are merged.

## Every UI story
- Build the screen with the tokens from `Tokens.axaml` and the view models; the board in `design/boards/` is a **visual reference only**: match spacing, type and colours, never copy its markup.
- Always the design system (CLAUDE.md rules 6 and 7); a deviation only after Nico said so, recorded in `docs/DESIGN-NOTES.md`.
- Keyboard use, focus states, screen reader names (Avalonia automation properties), contrast as in the web DESIGN.md.
- Window at the board's size (900 × 620) and resizable down to a sensible minimum.
- The screen gets its design checks (UP-03): headless screenshot baselines and key style checks.

## E1 Foundation

### UP-01 Solution skeleton and CI
- .NET 10 solution with the projects of `docs/ARCHITECTURE.md` Layering, Avalonia 11 app that opens an empty window and a tray icon.
- xUnit test projects; `dotnet format` with an `.editorconfig`; nullable and warnings-as-errors on.
- A rule that keeps Core free of Avalonia and OS APIs (an architecture test, e.g. with assembly references, or a project rule), plus a test for it.
- GitHub Actions on `windows-latest` for PRs and `develop`: restore, build, format check, test.
- `CLAUDE.md` Commands filled in; README with local setup; `.editorconfig`, `.gitignore` for .NET.
- Done when: the empty app starts, one Core test and the architecture test pass in CI.

### UP-02 Tokens, fonts and base styles
- `Tokens.axaml` with the web's tokens (web `docs/DESIGN.md` §3 and `src/styles/tokens.css`): colours, type scale, spacing, radii, shadows, durations. A test compares the values with the web's `tokens.css` (read from a copy committed here, refreshed by a small script) so the two can't drift silently.
- Fonts Marcellus, Marcellus SC and Inter embedded (OFL licences included), monospace for paths as on the boards.
- Base styles: window chrome, panels, buttons (gold, secondary), toggles, segmented control, status dot, badges, list rows; a hidden component gallery window for the design checks.
- Reduced motion: follows Windows' "animation effects" setting.

### UP-03 Design checks
- Avalonia.Headless screenshot tests for the gallery and every screen, at the board size; baselines in the repo, updated only with Nico's OK.
- Token lint: no raw colours, font sizes or durations in `.axaml` outside `Tokens.axaml` (test).
- Runs in CI; a changed screenshot fails the build and uploads the new images as an artifact.

## E2 Core

### UP-04 Site client and pairing
- Typed client for `UPLOADER-API.md`: pair start and poll, upload, me, disconnect; error mapping (offline, 401 revoked, 413, rejected reasons) to Core results. A fake server for tests.
- Pairing flow: start, open the browser at the verify URL, show the code, poll until approved, denied or expired; "Paste an upload token instead".
- Token store with DPAPI; "Disconnect" revokes on the site and deletes it.
- Unit tests for every outcome.

### UP-05 First-run setup
- Board `setup`: three steps Connect, Find World of Warcraft, How it runs.
- Find WoW: registry and the Battle.net launcher config, then common paths; the `_forever_` folder; every account under `WTF\Account` with or without `CommunitiesForever.lua` ("Ready" with the file's save time, or "No addon file yet: log in once with the addon enabled"); "Scan again", "Choose folder…". No character count (CLAUDE.md rule 1, decided with Nico).
- How it runs: upload automatically, start with Windows, close hides to tray, notifications (every upload, only problems, off).
- Settings v1 written at the end; the setup reopens when there is no token or no watched account.

### UP-06 Watch and upload loop
- The state machine of `docs/ARCHITECTURE.md` "The loop" in Core: watching, game running, file changed, settling 5 s, hash skip, uploading, uploaded, offline retry with back-off, token revoked, rejected newer format, game not found, paused.
- File watcher and WoW process detection as Windows adapters; several accounts independently.
- Local history (last 50 per account) with the site's summary line and report link.
- Unit tests for every transition with a fake clock, fake file system and fake site.

## E3 Screens

### UP-07 Main window
- Board `main-window`: status header with the state dot and line ("All caught up", "Last upload today at 21:14 · World of Warcraft isn't running"), Upload now, Open site; watched accounts with save and upload times and size; the three-step explainer; recent uploads with summary lines; "Full history on the site →"; connected-as line.
- The status states of board `states` shown in the header with their actions (Upload now, Skip this one, Try now, Reconnect, Scan again, Choose folder…).

### UP-08 Tray, notifications and autostart
- Board `tray`: tray icon per state (watching, uploading, paused, needs attention) with tooltip line; left click opens the window; right-click menu (status, Upload now, Pause uploads → 1 hour / until tomorrow, Open site, Show uploader, Quit).
- Windows toasts: "Uploaded · 5 levels gained" with "See what changed", "No connection: will try again", "Upload token no longer works" with Reconnect / Later; "Only problems" keeps the last two.
- Start with Windows (hidden in the tray), close hides to tray, single instance.

### UP-09 Settings
- Board `settings`: Behaviour (upload automatically, start with Windows, close to tray, notifications, pause), Game folders (scan again, add folder, remove), Connection (connected as, token created, Test connection, Disconnect), About (version, Check for updates, Open log folder).

## E4 Release

### UP-10 Packaging and updates
- Velopack: per-user installer and update packages; the app checks the site's feed (with its token) at start and daily, "Check for updates" in settings.
- Release workflow on tag `v*` on `main`: build, test, pack, publish installer and packages to the site (WEB-35's publish endpoint with a deploy secret).
- Not code-signed (decided with Nico); README explains the SmartScreen warning.
- Manual: install from the site on a clean PC, update to a newer tag.
