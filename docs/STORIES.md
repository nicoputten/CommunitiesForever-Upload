# Backlog

Open stories only, in build order. Done stories move to `docs/STORIES-COMPLETED.md`.
Each story: criteria are testable. "Manual" items are steps only Nico can do; they go in the PR.

Build order: UP-11, UP-08, UP-09, UP-10 (UP-01 to UP-07 are done). All of it is v1 (decided with Nico).
The site side is built in the web repo: WEB-33 (uploader API, merged), WEB-34 (uploader page), WEB-35 (installer hosting and update feed).

## Every UI story
- Build the screen with the tokens from `Tokens.axaml` and the view models; the board in `design/boards/` is a **visual reference only**: match spacing, type and colours, never copy its markup.
- Always the design system (CLAUDE.md rules 6 and 7); a deviation only after Nico said so, recorded in `docs/DESIGN-NOTES.md`.
- Keyboard use, focus states, screen reader names (Avalonia automation properties), contrast as in the web DESIGN.md.
- Window at the board's size (900 × 620) and resizable down to a sensible minimum.
- The screen gets its design checks (UP-03): headless screenshot baselines and key style checks.

## E3 Screens

### UP-11 Keep dependencies fresh
Built after UP-07 (small; like WEB-37 in the web repo).
- Dependabot (`.github/dependabot.yml`) for NuGet and GitHub Actions, weekly; minor and patch updates grouped into one PR, majors in their own.
- CI decides whether an update is safe (build, format, tests, design checks). A changed screenshot still needs Nico's OK (CLAUDE.md rule 7), so such an update waits for him.
- Every exact pin that Dependabot must not move (for example xunit.v3 3.x for Avalonia.Headless.XUnit) is listed with its reason in `docs/ARCHITECTURE.md`, and ignored in `dependabot.yml` with the same reason.

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
