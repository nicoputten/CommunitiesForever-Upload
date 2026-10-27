# Design notes

The look is the web's "Night over Azeroth" design system: tokens, type and data age come from the web repo's `docs/DESIGN.md` and `src/styles/tokens.css`. The app's screens are in `design/boards/` (index: `design/README.md`). This file holds what is specific to the app.

## Decisions
- App name: the community's name from the site after pairing ("Doombringers Uploader"), "CommunitiesForever Uploader" before (Nico, 2026-10-27).
- No character count in the setup scan: the app doesn't parse the file (CLAUDE.md rule 1, Nico, 2026-10-27).
- "Told Nico automatically" on the newer-format state is left out (Nico, 2026-10-27).

## Deviations
Approved departures from the boards (CLAUDE.md rule 7). Only Nico approves one; add it here in the PR that builds it.

| Date | What | Why | Approved |
|---|---|---|---|
| 2026-10-27 | Setup scan shows the file and its save time, no "10 characters" | No parsing in the app | Nico |
| 2026-10-27 | Newer-format state without "Told Nico automatically" | Left out | Nico |
