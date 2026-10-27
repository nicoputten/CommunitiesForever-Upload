# Backlog

Open stories only, in build order. Done stories move to `docs/STORIES-COMPLETED.md`.
Each story: criteria are testable. "Manual" items are steps only Nico can do; they go in the PR.

Build order: UP-09, UP-10, then UP-12 once the web's WEB-44 and the addon's CF-45 are released (UP-01 to UP-08 and UP-11 are done). All of it is v1 (decided with Nico).
The site side is built in the web repo: WEB-33 (uploader API, merged), WEB-34 (uploader page), WEB-35 (installer hosting and update feed).

## Every UI story
- Build the screen with the tokens from `Tokens.axaml` and the view models; the board in `design/boards/` is a **visual reference only**: match spacing, type and colours, never copy its markup.
- Always the design system (CLAUDE.md rules 6 and 7); a deviation only after Nico said so, recorded in `docs/DESIGN-NOTES.md`.
- Keyboard use, focus states, screen reader names (Avalonia automation properties), contrast as in the web DESIGN.md.
- Window at the board's size (900 × 620) and resizable down to a sensible minimum.
- The screen gets its design checks (UP-03): headless screenshot baselines and key style checks.

## E3 Screens

### UP-09 Settings
- Board `settings`: Behaviour (upload automatically, start with Windows, close to tray, notifications, pause), Game folders (scan again, add folder, remove), Connection (connected as, token created, Test connection, Disconnect), About (version, Check for updates, Open log folder).

## E4 Release

### UP-10 Packaging and updates
- Velopack: per-user installer and update packages; the app checks the site's feed (with its token) at start and daily, "Check for updates" in settings.
- Release workflow on tag `v*` on `main`: build, test, pack, publish installer and packages to the site (WEB-35's publish endpoint with a deploy secret).
- The repo may be public (decided with Nico, 2026-10-27): the release workflow triggers only on `v*` tags (never `pull_request` or `pull_request_target`), the deploy secret lives in a protected GitHub environment (`release`, only `main`, Nico approves), and nothing prints it. Fork PRs keep running CI without secrets.
- Not code-signed (decided with Nico); README explains the SmartScreen warning.
- Manual: install from the site on a clean PC, update to a newer tag.

## E5 Planned runs

### UP-12 Runs to the game
Planned runs (decided with Nico, 2026-10-27; the flow: board `runs-flow`): the site owns them, and the game learns about them from a file this app writes, loaded by the addon at login or `/reload`. Needs the web's WEB-44 (`GET /api/uploader/v1/runs`) and the addon's CF-45 (ships the empty file, so it exists when the game starts; a file added later loads only after a game restart).
- Fetch the upcoming runs every 5 minutes while World of Warcraft runs, and right after each upload; not while the game is closed (one fetch at game start).
- Write them to `Interface\AddOns\CommunitiesForever\Data\Runs.lua` of each game folder found (setup, UP-05), as Lua data only: one table `CF_RUNS = { updatedAt = <UTC>, runs = { ... } }` generated from the site's JSON with every string escaped and length-capped, never code or text from the site as code (CLAUDE.md rule 1's exception). Atomic: write a temp file next to it, then replace, so a `/reload` never reads half a file. Write only when the content changed.
- Not a SavedVariables file (the game would overwrite it on logout), and never the addon's saved file.
- When the addon folder or the file is missing (addon not installed, or an addon update replaced the folder): write it again when the folder exists; when the addon has no `Data` folder yet (a version before CF-45), write nothing and show "Update the addon to see runs in game".
- Main window: the board `main-window-runs` "Runs" block: "3 upcoming runs sent to the game · 17:34", "Checks every 5 min while WoW runs, and after each upload · next 17:39", "/reload to see them" while the game runs, "Open runs on the site". The board's other lines that are already deviations (`docs/DESIGN-NOTES.md`) stay as built.
- The recent-uploads summary shows the site's "N run answers" like its other counts.
- Tests: the Lua writer (escaping, caps, a site string that tries to close the table or inject code), the atomic replace, the schedule (game running, after upload, game closed), the missing-folder cases, no memory growth over a soak run (rule 11).
- Manual: plan a run on the site, wait for the fetch or upload, `/reload` in game, the Dungeons tab (addon CF-46) shows it.

