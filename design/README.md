# Design boards

Exports from the Claude Design workshop for the uploader, in the web's "Night over Azeroth" direction. Decisions: `docs/DESIGN-NOTES.md`; tokens: the web repo's `docs/DESIGN.md`.

The boards are **visual references only**, never production code: replicate their values in Avalonia with the tokens. Each folder is one export, unzipped unchanged; replace the folder when a board is re-exported. `support.js` and `vendor/` are the tool's runtime. Where a board and `docs/DESIGN-NOTES.md` disagree, the notes win.

View them: `python -m http.server 4400 --directory design`, then open http://localhost:4400/boards/ (the scripts don't load over file://).

| Folder | Board | Screen |
|---|---|---|
| `setup` | `AppSetup.dc.html` | First-run setup: connect (in the browser, OAuth device flow), find World of Warcraft, how it runs |
| `main-window` | `AppMain.dc.html` | Main window: status, watched accounts, explainer, recent uploads |
| `states` | `AppStates.dc.html` | The loop and every status state with its actions |
| `tray` | `AppTray.dc.html` | Tray icon per state, right-click menu, toast notifications |
| `settings` | `AppSettings.dc.html` | Behaviour, game folders, connection, about |
| `site-uploader-page` | `SiteUploader.dc.html` | The website's uploader page (built in the web repo, WEB-34; copied there too) |
| `main-window-runs` | `AppMain-runs.dc.html` | Main window with the Runs block: runs sent to the game, next check, the /reload hint (UP-12; a copy of `main-window`, which stays as it is) |
| `runs-flow` | `RunsFlow.dc.html` | Planned runs across game, uploader, site and Discord (overview; from the web repo) |

## Icons
Supplied by Nico on 2026-10-27 as `doombringers-uploader-icons.zip`: a shield with a cross in the "Night over Azeroth" colours.
- `src/CommunitiesForever.Upload.App/Assets/Icons/png/`: the supplied PNGs, unchanged.
  - App: `app-icon-<size>` at 40, 48, 64, 96, 128, 256 and `app-icon-small-<size>` (thicker lines) at 16, 20, 24, 32.
  - Tray: `tray`, `tray-paused`, `tray-attention`, `tray-uploading-1..4` (animation frames), each at 16, 20, 24, 32, 64.
- `design/icons/`: the supplied SVG sources and the preview sheet `doombringers-icons-preview.png` (dark and light taskbar), unchanged.
- The `.ico` files next to `png/` are built from the PNGs by `scripts/build-icons.ps1` (the zip's own `.ico` files are not used: they hold fewer sizes). `app.ico` is the exe, taskbar and window icon; the tray `.ico` files are for UP-08 (watching, uploading frames, paused, needs attention).
