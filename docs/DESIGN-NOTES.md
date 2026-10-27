# Design notes

The look is the web's "Night over Azeroth" design system: tokens, type and data age come from the web repo's `docs/DESIGN.md` and `src/styles/tokens.css`. The app's screens are in `design/boards/` (index: `design/README.md`). This file holds what is specific to the app.

## Decisions
- App name: the community's name from the site after connecting ("Doombringers Uploader"), "CommunitiesForever Uploader" before (Nico, 2026-10-27).
- No character count in the setup scan: the app doesn't parse the file (CLAUDE.md rule 1, Nico, 2026-10-27).
- "Told Nico automatically" on the newer-format state is left out (Nico, 2026-10-27).
- Tokens (UP-02, Nico, 2026-10-27): `src/CommunitiesForever.Upload.App/Styles/Tokens.axaml` mirrors Part 1 and the general blocks of Part 2 of the web's `tokens.css` (up to the page-specific blocks). Names are the CSS name in PascalCase (`--text-muted` is `TextMuted`, `--s-0-5` is `S0_5`), colours also as `<Name>Brush`, spacing also as `<Name>Thickness`. `TokenDriftTests` compares them with `design/tokens/web-tokens.css`, refreshed by `scripts/refresh-tokens.ps1`.
- App tokens: values from the app boards that the web has no token for are prefixed `App` in `Tokens.axaml` (window and title bar sizes, switch, segment padding, status dot, composed paddings, the window background glow). Fonts: Marcellus SC, Marcellus and Inter embedded (OFL), Consolas for paths and codes.
- Letter spacing and line height stay in em as on the web; `Typography.LetterSpacingEm` / `LineHeightEm` turn them into pixels for the TextBlock's font size.
- Window chrome: the app draws the board's title bar (emblem, name, minimise, close); Windows still moves, snaps and resizes the window. Until UP-08 the close button quits and is named "Close"; UP-08 makes it "Hide to tray" as on the board.
- Icons: Nico's icon set (2026-10-27, source in `design/README.md` "Icons"). Exe, taskbar and window: `app.ico`; title bar emblem: `app-icon-small`; tray: `tray.ico` (watching) until UP-08 adds the other states.
- Setup (UP-05, Nico, 2026-10-27): connecting starts as soon as step 1 opens and moves on to step 2 by itself once allowed; Back from step 2 shows step 1 as "Connected as …". Setup opens at step 1 without a token, at step 2 with a token but no watched account. Cancel in step 1 quits. The four "How it runs" options are stored by setup; UP-08 makes them work.
- Where the setup board differs between its own screens and the main-window and settings boards by a pixel (button padding 18 vs 16, segment padding 6/10 vs 5/9, row line 0.06 vs 0.05, account fill 0.03 vs 0.025), the app uses one value: the existing token.
- Main window (UP-07, Nico, 2026-10-27): the header shows the most urgent account (named in the text when several are watched). The rings pulse while all is well; problem states use the states board's "warn" look. "Upload now" with everything up to date sends the current file again (the site answers "Nothing new"). "Reconnect" opens setup at step 1, "Change…", "Scan again" and "Choose folder…" at step 2; Cancel there returns to the main window. When not connected the status bar says so.
- Badges follow the web's data-age badge (roster cards): neutral, fresh, stale.
- Reduced motion follows Windows' "Animation effects" (checked at start and whenever the window is activated); off removes every transition.

## Deviations
Approved departures from the boards (CLAUDE.md rule 7). Only Nico approves one; add it here in the PR that builds it.

| Date | What | Why | Approved |
|---|---|---|---|
| 2026-10-27 | Setup scan shows the file and its save time, no "10 characters" | No parsing in the app | Nico |
| 2026-10-27 | Newer-format state without "Told Nico automatically" | Left out | Nico |
| 2026-10-27 | Setup before connecting: title "CommunitiesForever Uploader" and heading "Connect to wow.vestras.nl" instead of "Doombringers" | The site's name is only known after connecting | Nico |
| 2026-10-27 | Setup step 1 states not on the board: getting a code, code expired, denied on the site, site unreachable (one stale line and "Try again" in the code card), connected (after Back); paste errors inline under the field | The board shows only "waiting" | Nico |
| 2026-10-27 | Setup step 2 "nothing found": "Where is World of Warcraft?" with a short explanation, Scan again and Choose folder…; "Looking for your game…" while scanning | Not on the setup board; wording from the states board | Nico |
| 2026-10-27 | Main window header texts not on the boards: Ready to upload (automatic uploads off), Paused until …, Not allowed (403), and one title and line per rejection reason (wrong file, damaged, someone else's file, another community, site not set up yet, too large, empty) | The boards show only the board states | Nico |
| 2026-10-27 | Newer format: "Your addon writes data format 3; the site can't read it yet" instead of "the site reads up to 2"; uploading shows the size without a character count | The site only tells the file's format; no parsing (rule 1) | Nico |
| 2026-10-27 | "Uploaded" header (the states board's "Done": the first count as title, the highlights below) shows for an hour after an upload, then "All caught up" | When the board's two headers apply was not drawn | Nico |
| 2026-10-27 | Recent uploads show the site's summary lines only (last 7, all accounts), not local notes like "Offline, sent 2 min later"; "Full history on the site →" opens /upload | The app doesn't keep such notes | Nico |
| 2026-10-27 | The status bar's "Settings" link is hidden until UP-09 builds the settings window | Nothing to open yet | Nico |
| 2026-10-27 | No backdrop blur (`--blur-surface`, `--blur-modal`) and no glow tokens; see-through surfaces are drawn as their rgba colour over the window background | Avalonia can't blur what is behind a control inside a window; the app has no map behind its panels | Nico |
