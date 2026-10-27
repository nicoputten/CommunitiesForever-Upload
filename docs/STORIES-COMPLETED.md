# Done stories

### UP-01 Solution skeleton and CI
- .NET 10 solution with the projects of `docs/ARCHITECTURE.md` Layering, Avalonia 11 app that opens an empty window and a tray icon.
- xUnit test projects; `dotnet format` with an `.editorconfig`; nullable and warnings-as-errors on.
- A rule that keeps Core free of Avalonia and OS APIs (an architecture test, e.g. with assembly references, or a project rule), plus a test for it.
- GitHub Actions on `windows-latest` for PRs and `develop`: restore, build, format check, test.
- `CLAUDE.md` Commands filled in; README with local setup; `.editorconfig`, `.gitignore` for .NET.
- Done when: the empty app starts, one Core test and the architecture test pass in CI.
- Done (PR #1): `CommunitiesForever.Upload.slnx` with Core (`net10.0`), Windows and App (`net10.0-windows10.0.19041.0`), Avalonia 12; exact versions in `Directory.Packages.props`; `ArchitectureTests` read Core's metadata; CI on `windows-latest`. Smart App Control on Nico's PC blocked unsigned builds (now off; signing is an open point for UP-10). Rule 11 (no memory leaks) added to CLAUDE.md.

### UP-02 Tokens, fonts and base styles
- `Tokens.axaml` with the web's tokens (web `docs/DESIGN.md` §3 and `src/styles/tokens.css`): colours, type scale, spacing, radii, shadows, durations. A test compares the values with the web's `tokens.css` (read from a copy committed here, refreshed by a small script) so the two can't drift silently.
- Fonts Marcellus, Marcellus SC and Inter embedded (OFL licences included), monospace for paths as on the boards.
- Base styles: window chrome, panels, buttons (gold, secondary), toggles, segmented control, status dot, badges, list rows; a hidden component gallery window for the design checks.
- Reduced motion: follows Windows' "animation effects" setting.
- Done (PR #2): `Styles/Tokens.axaml` mirrors the web's Part 1 and general Part 2 tokens (`TokenDriftTests` against `design/tokens/web-tokens.css`); fonts embedded with OFL; control themes and base styles; own title bar; `GalleryWindow` (`--gallery`); reduced motion via `IMotionPreference`; Nico's icon set and `scripts/build-icons.ps1`. Approved deviation: no backdrop blur.

### UP-03 Design checks
- Avalonia.Headless screenshot tests for the gallery and every screen, at the board size; baselines in the repo, updated only with Nico's OK.
- Token lint: no raw colours, font sizes or durations in `.axaml` outside `Tokens.axaml` (test).
- Runs in CI; a changed screenshot fails the build and uploads the new images as an artifact.
- Done (PR #3): `tests/…App.DesignChecks` with Skia screenshots against `Baselines/` (CI renders them, Nico approves before they are committed); `TokenLintTests`, `ContrastTests`. Found the dashed Fluent focus ring. Pairing docs moved to the OAuth device flow.

### UP-04 Site client and connecting
- Typed client for `UPLOADER-API.md`: device authorization and token polling (RFC 8628), upload, identity, revoke (RFC 7009); error mapping of the Bearer errors (RFC 6750) and the site's codes (offline, revoked token, may not upload, too large, rejected reasons) to Core results. A fake server for tests that follows the document's examples.
- Connect flow (RFC 8628): request a device code, open the browser at the complete verification URL, show the user code, poll at the given interval (`slow_down` adds 5 s) until allowed, denied or expired; the device code stays in memory only. "Paste an upload token instead".
- Token store with DPAPI; "Disconnect" revokes on the site and deletes it.
- Unit tests for every outcome.
- Done (PR #4): `SiteClient` (Duende.IdentityModel for the OAuth calls) with `SiteResult`/`SiteFailure`, `Secret` types that never print, `ConnectFlow` (RFC 8628 polling on `TimeProvider`), `DpapiTokenStore`; tests answer with the contract's own examples (`docs/contract/UPLOADER-API.md`, `scripts/refresh-api-contract.ps1`). `tools/FakeSite` dropped.

### UP-05 First-run setup
- Board `setup`: three steps Connect, Find World of Warcraft, How it runs.
- Find WoW: registry and the Battle.net launcher config, then common paths; the `_forever_` folder; every account under `WTF\Account` with or without `CommunitiesForever.lua` ("Ready" with the file's save time, or "No addon file yet: log in once with the addon enabled"); "Scan again", "Choose folder…". No character count (CLAUDE.md rule 1, decided with Nico).
- How it runs: upload automatically, start with Windows, close hides to tray, notifications (every upload, only problems, off).
- Settings v1 written at the end; the setup reopens when there is no token or no watched account.
- Done (PR #5): `SetupWindow` with three steps, settings v1 (`SettingsStore`, migration chain), `GameScanner` over `IFileSystem`/`IInstallHints` (registry, Battle.net.config, usual folders); startup opens setup without a token or account. Approved deviations: pre-connect title, connect error states, "nothing found".

### UP-06 Watch and upload loop
- The state machine of `docs/ARCHITECTURE.md` "The loop" in Core: watching, game running, file changed, settling 5 s, hash skip, uploading, uploaded, offline retry with back-off, token revoked, rejected newer format, game not found, paused.
- File watcher and WoW process detection as Windows adapters; several accounts independently.
- Local history (last 50 per account) with the site's summary line and report link.
- Upload ledger per account: every finished upload is recorded by its SHA-256 with its outcome (`ok`, `nothing_new`, or `rejected` with the reason). A file whose hash is in the ledger is never uploaded again. Bounded (the last few hundred hashes) and versioned like the settings (CLAUDE.md rule 10). Decided with Nico: `newer_format` is tried again at the next app start or after 24 h, `no_community_yet` after 1 h; see ARCHITECTURE "The loop".
- Unit tests for every transition with a fake clock, fake file system and fake site.
- Done (PR #6): `UploadLoop` (one worker per account, settle, ledger, one upload at a time), `UploadHistory` (`history.json` v1: ledger 300, history 50), retries as decided (newer_format at the next start or after 24 h), FileSystemWatcher + 30 s poll, rolling logs; soak test of 10,000 saves. Fixed two "Upload now" bugs the soak test found.

### UP-07 Main window
- Board `main-window`: status header with the state dot and line ("All caught up", "Last upload today at 21:14 · World of Warcraft isn't running"), Upload now, Open site; watched accounts with save and upload times and size; the three-step explainer; recent uploads with summary lines; "Full history on the site →"; connected-as line.
- The status states of board `states` shown in the header with their actions (Upload now, Skip this one, Try now, Reconnect, Scan again, Choose folder…).
- Done (PR #7): `MainWindow` with the status header (`Hero`), watched accounts, recent uploads and status bar over `IUploadLoop`; after Nico tried it: less text, the wizard only on first run with "Watched accounts" and "Connect" windows instead, "Scan again"/"Choose folder…" in place, pulse centred. Deviations in DESIGN-NOTES.

### UP-11 Keep dependencies fresh
Built after UP-07 (small; like WEB-37 in the web repo).
- Dependabot (`.github/dependabot.yml`) for NuGet and GitHub Actions, weekly; minor and patch updates grouped into one PR, majors in their own.
- CI decides whether an update is safe (build, format, tests, design checks). A changed screenshot still needs Nico's OK (CLAUDE.md rule 7), so such an update waits for him.
- Every exact pin that Dependabot must not move (for example xunit.v3 3.x for Avalonia.Headless.XUnit) is listed with its reason in `docs/ARCHITECTURE.md`, and ignored in `dependabot.yml` with the same reason.
- Done (PR #8): `.github/dependabot.yml` (NuGet and GitHub Actions, weekly into `develop`; groups `avalonia`, `dotnet`, `minor-and-patch`; xunit.v3 and the .NET 10 packages pinned to their major), "Dependencies" in ARCHITECTURE. No auto-merge.

### UP-08 Tray, notifications and autostart
- Board `tray`: tray icon per state (watching, uploading, paused, needs attention) with tooltip line; left click opens the window; right-click menu (status, Upload now, Pause uploads → 1 hour / until tomorrow, Open site, Show uploader, Quit).
- Windows toasts: "Uploaded · 5 levels gained" with "See what changed", "No connection: will try again", "Upload token no longer works" with Reconnect / Later; "Only problems" keeps the last two.
- Start with Windows (hidden in the tray), close hides to tray, single instance.
- Done (PR #11): `TrayController` (icon per state with the uploading animation, tooltip, menu with pause until 06:00 and resume), `NotificationPolicy` (Core) and `NotificationTexts`, Windows toasts through `ShellRegistration` (sender and the `communitiesforever-uploader://` link, no COM server) and `StartMenuShortcut` (the header's name and shield), `SingleInstance`, `RegistryAutostart` (`--tray`), close hides to the tray. Deviations: the tray menu and notifications in Windows' own style.

### UP-09 Settings
- Board `settings`: Behaviour (upload automatically, start with Windows, close to tray, notifications, pause), Game folders (scan again, add folder, remove), Connection (connected as, token created, Test connection, Disconnect), About (version, Check for updates, Open log folder).
- Done (PR #13): `SettingsView` as a page in the main window (left menu jumps to a section, "⚙ Settings" in the status bar), `SettingsViewModel` saves right away; the app applies autostart and the close button, and restarts watching only when accounts or "upload automatically" change. Game folders replace the "Watched accounts" window (switch per account, Scan again, Add folder…, "not found"); Disconnect asks inline, revokes and deletes the token. "Check for updates" waits for UP-10. Deviations in DESIGN-NOTES.

### UP-10 Packaging and updates
- Velopack: per-user installer and update packages; the app checks the site's feed (with its token) at start and daily, "Check for updates" in settings.
- Release workflow on tag `v*` on `main`: build, test, pack, publish installer and packages to the site (WEB-35's publish endpoint with a deploy secret).
- The repo may be public (decided with Nico, 2026-10-27): the release workflow triggers only on `v*` tags (never `pull_request` or `pull_request_target`), the deploy secret lives in a protected GitHub environment (`release`, only `main`, Nico approves), and nothing prints it. Fork PRs keep running CI without secrets.
- Not code-signed (decided with Nico); README explains the SmartScreen warning.
- Manual: install from the site on a clean PC, update to a newer tag.
- Done (PR #14): self-contained Velopack packaging (pack id `CommunitiesForever.Uploader`, own shortcut), the release workflow on `v*` tags in the `release` environment with a signed feed (ECDSA P-256), `UpdateChecker` with the boards' states (quiet download, install on quit, at start or when WoW is closed; never mid-upload; retry in an hour; failed install; required updates hold uploads), settings v2 ("Install updates when WoW is closed"), Settings › About & updates, "Updated to … · What's new" in the footer, the required-update banner and tray item, uninstall cleanup, README install guide (SmartScreen, Smart App Control). Needs the web side for `GET updates/status`, the `.sig` file and the What's new page (proposed in the PR).

### UP-13 The feed from the site before packing
Nico, 2026-10-27: "the ideal route", the Velopack way; with web WEB-35b (the site keeps the signed feed as sent).
- Before `vpk pack` the release workflow pulls the site's current feed and its newest full package, so pack builds a delta and a complete feed; then it signs the feed and publishes (with the delta when there is one).
- The first release has nothing to pull (404) and still works.
- How many versions stay is decided here: the pulled feed is trimmed to the 2 newest earlier versions (3 with the new one); the site keeps exactly what the feed lists.
- Manual: the next release after this one is the first to pull and make a delta; check its log ("Pulled the feed … and …-full.nupkg") and that an installed app updates.
- Done (PR #PRNUM).
