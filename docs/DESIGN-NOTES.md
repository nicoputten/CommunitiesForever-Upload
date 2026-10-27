# Design notes

The look is the web's "Night over Azeroth" design system: tokens, type and data age come from the web repo's `docs/DESIGN.md` and `src/styles/tokens.css`. The app's screens are in `design/boards/` (index: `design/README.md`). This file holds what is specific to the app.

## Decisions
- App name: the community's name from the site after pairing ("Doombringers Uploader"), "CommunitiesForever Uploader" before (Nico, 2026-10-27).
- No character count in the setup scan: the app doesn't parse the file (CLAUDE.md rule 1, Nico, 2026-10-27).
- "Told Nico automatically" on the newer-format state is left out (Nico, 2026-10-27).
- Tokens (UP-02, Nico, 2026-10-27): `src/CommunitiesForever.Upload.App/Styles/Tokens.axaml` mirrors Part 1 and the general blocks of Part 2 of the web's `tokens.css` (up to the page-specific blocks). Names are the CSS name in PascalCase (`--text-muted` is `TextMuted`, `--s-0-5` is `S0_5`), colours also as `<Name>Brush`, spacing also as `<Name>Thickness`. `TokenDriftTests` compares them with `design/tokens/web-tokens.css`, refreshed by `scripts/refresh-tokens.ps1`.
- App tokens: values from the app boards that the web has no token for are prefixed `App` in `Tokens.axaml` (window and title bar sizes, switch, segment padding, status dot, composed paddings, the window background glow). Fonts: Marcellus SC, Marcellus and Inter embedded (OFL), Consolas for paths and codes.
- Letter spacing and line height stay in em as on the web; `Typography.LetterSpacingEm` / `LineHeightEm` turn them into pixels for the TextBlock's font size.
- Window chrome: the app draws the board's title bar (emblem, name, minimise, close); Windows still moves, snaps and resizes the window. Until UP-08 the close button quits and is named "Close"; UP-08 makes it "Hide to tray" as on the board.
- Icons: Nico's icon set (2026-10-27, source in `design/README.md` "Icons"). Exe, taskbar and window: `app.ico`; title bar emblem: `app-icon-small`; tray: `tray.ico` (watching) until UP-08 adds the other states.
- Badges follow the web's data-age badge (roster cards): neutral, fresh, stale.
- Reduced motion follows Windows' "Animation effects" (checked at start and whenever the window is activated); off removes every transition.

## Deviations
Approved departures from the boards (CLAUDE.md rule 7). Only Nico approves one; add it here in the PR that builds it.

| Date | What | Why | Approved |
|---|---|---|---|
| 2026-10-27 | Setup scan shows the file and its save time, no "10 characters" | No parsing in the app | Nico |
| 2026-10-27 | Newer-format state without "Told Nico automatically" | Left out | Nico |
| 2026-10-27 | No backdrop blur (`--blur-surface`, `--blur-modal`) and no glow tokens; see-through surfaces are drawn as their rgba colour over the window background | Avalonia can't blur what is behind a control inside a window; the app has no map behind its panels | Nico |
