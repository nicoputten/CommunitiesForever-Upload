# Changelog

## v0.3.0 (2026-10-29)
- Retail test mode for testing the addon before Forever launches: started with `--retail`, the uploader watches and writes the retail client's folder instead of Forever's, with its own settings, and says "Retail test mode" in the window. Without the flag nothing changes.

## v0.2.0 (2026-10-28)
- Uses far less memory, most of all in the tray: the window is drawn without the graphics card, and the app gives memory back a few seconds after you hide it to the tray.
- Planned runs reach the game: while World of Warcraft runs (every 5 minutes) and after each upload, the uploader gets the upcoming runs from the site and writes them into the addon, so the Dungeons tab shows them after a /reload. The main window has a Runs block saying how many were sent, when it checks next, and when the game needs a /reload, a restart or a newer addon.

## v0.1.0 (2026-10-27)
- Updates download only what changed since your version, when you're at most two versions behind; otherwise the whole app as before.
- Installs from the site and keeps itself up to date: it checks once a day, downloads quietly and installs when you quit, when Windows restarts or when World of Warcraft is closed (a setting). Never during an upload, no pop-ups. Settings › About & updates shows what it's doing; after an update the window says so once, with a link to what's new.
- Settings (the gear in the status bar): upload automatically, start with Windows, close to the tray, notifications, pause for an hour or until tomorrow; which accounts to watch (scan again or add a folder); the connection with Test connection and Disconnect; the version and the log folder. Changes take effect right away.
- Lives in the tray: the icon shows what it is doing, right-click to upload, pause, open the site or quit. Windows notifications after an upload and when something needs you. Starts with Windows and closes to the tray, if you want (setup step 3).
- Main window: what the uploader is doing right now (with Upload now, Try now, Reconnect and more), your watched accounts, and your recent uploads with links to their session reports.
- Uploads by itself: after the game saves the addon file (logout, /reload or quitting) and it has been unchanged for 5 seconds. Never sends the same file twice, keeps trying while offline, and keeps a log in the app's folder.
- First-run setup: connect in the browser (or paste a token), find World of Warcraft: Forever and its accounts, and choose how the app runs.
- The window has the "Night over Azeroth" look of the site: its own title bar, the site's colours and fonts. Animations follow Windows' "Animation effects" setting.
- Keyboard focus shows a gold ring around the focused control.
