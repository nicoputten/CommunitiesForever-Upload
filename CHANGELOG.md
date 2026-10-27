# Changelog

## Unreleased

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
