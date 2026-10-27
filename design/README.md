# Design boards

Exports from the Claude Design workshop for the uploader, in the web's "Night over Azeroth" direction. Decisions: `docs/DESIGN-NOTES.md`; tokens: the web repo's `docs/DESIGN.md`.

The boards are **visual references only**, never production code: replicate their values in Avalonia with the tokens. Each folder is one export, unzipped unchanged; replace the folder when a board is re-exported. `support.js` and `vendor/` are the tool's runtime. Where a board and `docs/DESIGN-NOTES.md` disagree, the notes win.

View them: `python -m http.server 4400 --directory design`, then open http://localhost:4400/boards/ (the scripts don't load over file://).

| Folder | Board | Screen |
|---|---|---|
| `setup` | `AppSetup.dc.html` | First-run setup: connect (browser pairing), find World of Warcraft, how it runs |
| `main-window` | `AppMain.dc.html` | Main window: status, watched accounts, explainer, recent uploads |
| `states` | `AppStates.dc.html` | The loop and every status state with its actions |
| `tray` | `AppTray.dc.html` | Tray icon per state, right-click menu, toast notifications |
| `settings` | `AppSettings.dc.html` | Behaviour, game folders, connection, about |
| `site-uploader-page` | `SiteUploader.dc.html` | The website's uploader page (built in the web repo, WEB-34; copied there too) |
