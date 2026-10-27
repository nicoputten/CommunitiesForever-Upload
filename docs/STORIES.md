# Backlog

Open stories only, in build order. Done stories move to `docs/STORIES-COMPLETED.md`.
Each story: criteria are testable. "Manual" items are steps only Nico can do; they go in the PR.

Build order: UP-10 (UP-01 to UP-09 and UP-11 are done). All of it is v1 (decided with Nico).
The site side is built in the web repo: WEB-33 (uploader API, merged), WEB-34 (uploader page), WEB-35 (installer hosting and update feed).

## Every UI story
- Build the screen with the tokens from `Tokens.axaml` and the view models; the board in `design/boards/` is a **visual reference only**: match spacing, type and colours, never copy its markup.
- Always the design system (CLAUDE.md rules 6 and 7); a deviation only after Nico said so, recorded in `docs/DESIGN-NOTES.md`.
- Keyboard use, focus states, screen reader names (Avalonia automation properties), contrast as in the web DESIGN.md.
- Window at the board's size (900 × 620) and resizable down to a sensible minimum.
- The screen gets its design checks (UP-03): headless screenshot baselines and key style checks.

## E4 Release

### UP-10 Packaging and updates
- Velopack: per-user installer and update packages; the app checks the site's feed (with its token) at start and daily, "Check for updates" in settings.
- Release workflow on tag `v*` on `main`: build, test, pack, publish installer and packages to the site (WEB-35's publish endpoint with a deploy secret).
- The repo may be public (decided with Nico, 2026-10-27): the release workflow triggers only on `v*` tags (never `pull_request` or `pull_request_target`), the deploy secret lives in a protected GitHub environment (`release`, only `main`, Nico approves), and nothing prints it. Fork PRs keep running CI without secrets.
- Not code-signed (decided with Nico); README explains the SmartScreen warning.
- Manual: install from the site on a clean PC, update to a newer tag.
