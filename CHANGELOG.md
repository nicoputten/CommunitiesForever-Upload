# Changelog

## v0.3.4 (2026-11-05)
- The main window fits at every size: in a narrow window the status card's text no longer squeezes to a few letters wide and its buttons no longer stick out. The buttons move under the text when the card is narrow, Recent uploads moves under the card in a narrow window, and the window scrolls instead of covering the status bar.

## v0.3.3 (2026-11-02)
- The upload history is no longer lost when a virus scanner holds its file while the uploader starts: it reads the file again, and never overwrites a history it couldn't read.

## v0.3.2 (2026-11-02)
- Notices World of Warcraft running when it was started as administrator, so planned runs reach the game and the window says "Playing" then too.
- An account no longer stops uploading until a restart when something unexpected goes wrong once, such as a virus scanner briefly holding the uploader's history file: it tries again at the next check.
- Settings › About & updates no longer stays on "Checking…" (with no way to check again) until the next day when the update check fails in an unusual way: it says it couldn't check and tries again in an hour.
- The uploader no longer closes by itself when Windows briefly refuses to list running programs or a virus scanner holds its settings file during a background check: the check is tried again a little later.

## v0.3.1 (2026-10-29)
- The Runs block in the main window says "3 runs saved for the game" and how to load them ("Type /reload to load them"), instead of "/reload to see them". The file path moved into a tooltip.

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
