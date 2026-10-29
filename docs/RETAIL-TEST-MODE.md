# Retail test mode

For testing the addon on the retail client before WoW: Forever launches. Normal users never need it; without the flag the app works in `_forever_` as always.

Started with `--retail`, the app works in the `_retail_` folder of the World of Warcraft install instead of `_forever_`:
- it finds and watches `_retail_\WTF\Account\<ACCOUNT>\SavedVariables\CommunitiesForever.lua` and uploads it (asks, answers, plans, records) exactly as in normal mode: same site, same token, same requests
- it writes the planned runs to `_retail_\Interface\AddOns\CommunitiesForever\Data\Runs.lua`
- the window title and status bar say "Retail test mode"
- it keeps its own settings and history in `%LocalAppData%\CommunitiesForever.Upload\retail-test\`, so the normal settings and watched accounts stay untouched; the token is shared, so it skips connecting and opens at "Find your game" the first time
- it leaves "Start with Windows" alone

## Switching it on
1. Quit the uploader (tray icon › Quit). A running uploader ignores a second start, flag or not.
2. Start it with the flag, in PowerShell:
   ```powershell
   & "$env:LOCALAPPDATA\CommunitiesForever.Uploader\current\CommunitiesForever.Upload.App.exe" --retail
   ```
   From the source: `dotnet run --project src/CommunitiesForever.Upload.App -- --retail`.
3. The first time, choose the retail account(s) to watch.

## Switching it off
Quit the uploader (tray icon › Quit) and start it normally (Start menu). Windows' autostart and the restart after an update also start it normally.
