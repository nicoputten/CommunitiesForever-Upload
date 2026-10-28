# Architecture

## Core rules
- The app is a courier: it finds the addon file, waits until the game has finished writing it, uploads the bytes and shows the site's answer. It never interprets the file (CLAUDE.md rule 1).
- One upload token per installed app, upload-only, created by connecting through the browser (OAuth 2.0 device authorization flow, RFC 8628) or pasted by hand, revocable on the site or by the app (RFC 7009).
- It runs quietly: tray first, window on demand, notifications by preference.
- Everything works offline: a file that couldn't be sent is kept and sent later.

## Stack
| Part | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | Nico's choice |
| UI | Avalonia 12 (MVVM with CommunityToolkit.Mvvm), compiled bindings | styleable to the shared tokens, headless rendering for screenshot tests |
| Tray icon | Avalonia `TrayIcon` | built in |
| Notifications | Windows toast notifications through the WinRT API of the `net10.0-windows10.0.19041.0` target | no package (UP-08) |
| HTTP | one shared `HttpClient` (`SiteHttp.Create`, 15 min connection lifetime, 100 s timeout), `SiteClient` in Core | CLAUDE.md rule 11 |
| OAuth (connecting, revoking) | Duende.IdentityModel 8.1.0 (Apache-2.0): device authorization, device token, revocation | named by UPLOADER-API.md "Standards" |
| Dependency injection | Microsoft.Extensions.DependencyInjection, container built only in App | interfaces everywhere, Core and Windows each offer one `AddCore()` / `AddWindowsAdapters()` |
| Logging | Microsoft.Extensions.Logging (Core sees `ILogger` only), small rolling file sink of our own in Windows | no Serilog; a test checks no token reaches the log |
| Time | `TimeProvider` (BCL) for every timer in Core; `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) in tests | settle, retry and pause tests run instantly |
| JSON | System.Text.Json with source generation | no package |
| Token storage | DPAPI (`System.Security.Cryptography.ProtectedData` 10.0.14, current user, app entropy), `token.bin` | CLAUDE.md rule 4 |
| Packaging and updates | Velopack 1.2.161 (NuGet, and the `vpk` tool at the same version in the release workflow) | per-user install without admin rights, feed served by the site (WEB-35) |
| Tests | xUnit v3, Avalonia.Headless for UI and screenshots, hand-written fakes, plain `Assert` | no mocking or assertion library |
| CI | GitHub Actions on `windows-latest` | |

Every package at an exact version. Anything not listed needs Nico's OK (CLAUDE.md rule 9).

### Dependencies (UP-11)
- Dependabot (`.github/dependabot.yml`) checks NuGet and GitHub Actions every Monday and opens PRs into `develop`: minor and patch updates grouped into one PR a week, majors on their own. Avalonia's four packages always move together (majors included); the .NET extensions and DPAPI form one group.
- CI decides whether an update is safe (build, format, tests, design checks); Nico merges, no auto-merge. An update that changes a screenshot fails the design checks and waits for his OK on the look (CLAUDE.md rule 7).
- Pins (ignored in `dependabot.yml` with the same reason; keep both lists in step):
  - `xunit.v3` stays on 3.x: Avalonia.Headless.XUnit 12.x is built against it. Lift when Avalonia moves to xunit.v3 4.
  - `Microsoft.Extensions.*` and `System.Security.Cryptography.ProtectedData` stay on 10.x: 11.x belongs to .NET 11, and the app is on .NET 10 LTS. Moving .NET is a deliberate story.
  - The SDK (`global.json`, `rollForward: latestFeature`) is outside Dependabot; it takes 10.0 feature bands and patches by itself.
- Versions stay exact (`[x.y.z]`, CLAUDE.md rule 9). If Dependabot's first run can't update the bracket notation, switch to plain `Version="x.y.z"` (with central package management NuGet restores exactly that version) and note it here.

### Techniques (agreed with Nico, 2026-10-27)
- No generic Host: plain DI plus one class of our own that starts and stops the loop; settings stay in our versioned `settings.json`.
- Retry and backoff are our own small state machine in Core, not Polly.
- File watching: `FileSystemWatcher` as the trigger plus a 30 s poll as the safety net; both feed the same settle step.
- Fake site: a fake `HttpMessageHandler` in unit tests whose answers are the `<!-- example -->` blocks of `docs/contract/UPLOADER-API.md` (a copy of the web's contract, refreshed by `scripts/refresh-api-contract.ps1`). By hand: the web dev server, started with `--site http://localhost:3000` (Debug builds). No `tools/FakeSite` (dropped in UP-04: the real API exists).
- Windows bits without packages: named mutex + named pipe for single instance, HKCU `Run` key for autostart, the WinRT notification API through the `net10.0-windows10.0.19041.0` target framework for toasts.
- Startup: ReadyToRun on publish; no Native AOT.
- Core targets plain `net10.0`, so Windows APIs are not even available there; `ArchitectureTests` in Core.Tests reads Core's metadata and fails on Avalonia, Windows APIs, the other projects, direct file system, process, registry or socket use and P/Invoke. `HttpClient` is allowed in Core but always injected.

## Tray, notifications, start with Windows (UP-08)
- The app lives in the tray (`ShutdownMode.OnExplicitShutdown`): it ends by the tray's Quit, or by closing the window when "Close hides to the tray" is off. `TrayController` (App) shows the icon per state, the tooltip and the menu, and turns status changes into notifications through `NotificationPolicy` (Core: an upload with something new, each problem once when it starts; the notification setting filters).
- Notifications are Windows toasts for this unpackaged app, without a package: `ShellRegistration` registers, at every start and for the current user only, the sender (`HKCU\Software\Classes\AppUserModelId\CommunitiesForever.Uploader`, name and icon) and the app link scheme `communitiesforever-uploader://` (`open`, `reconnect`). Every toast click is a link (protocol activation), so no COM server is needed. Windows takes the notification's header (name and shield) from a Start menu shortcut carrying the AppUserModelId, not from the registry: `StartMenuShortcut` keeps "CommunitiesForever Uploader.lnk" right at every start (found when Nico tried it; without it the header shows the raw id).
- One instance per Windows user (`SingleInstance`: named mutex and pipe). A later start (Start menu, a notification's link) hands its arguments to the running app and quits.
- Start with Windows: `RegistryAutostart` keeps `HKCU\…\Run\CommunitiesForever Uploader = "<exe>" --tray` in step with the setting; `--tray` starts hidden. Debug builds leave the Run key alone. The Run entry and the registrations point at Velopack's stable launcher and are removed on uninstall (UP-10).

## Long running (CLAUDE.md rule 11)
The app lives for weeks in the tray, so memory must stay flat:
- Whoever creates something with a lifetime disposes it: timers, watchers, `CancellationTokenSource`s, DI scopes, the tray icon. App disposes the service provider on exit.
- Every subscribed event handler is unsubscribed (or a weak event is used); no static events.
- One shared `HttpClient`; no caches or collections without a maximum (history 50 per account, a bounded queue, rolling logs).
- Tests: every view, view model and loop component that can be created more than once is checked to be collected after close or stop (`WeakReference` + GC); the loop has a soak test (`UploadLoopSoakTests`, 10,000 saves).
- Small in the tray (UP-14): Avalonia renders in software (`Program`), because the GPU path (ANGLE on Direct3D) held most of the memory, also with the window hidden; and `TrayMemory` runs one compacting collection a few seconds after the window hides to the tray, giving back the heap the window grew. Hiding keeps the window (reopening stays instant); disposing it saved only a few MB more.

## Layering
```
src/CommunitiesForever.Upload.Core/     pure: state machine, settle timer, hashing, retry and queue, connect flow,
                                        settings and history models with migrations, API contract types; no UI, no OS
src/CommunitiesForever.Upload.Windows/  thin adapters: WoW folder discovery (registry, Battle.net config), file watcher,
                                        autostart, DPAPI token store, toasts, single-instance, Runs.lua writer, Velopack
src/CommunitiesForever.Upload.App/      Avalonia app: views, view models, tray, styles (Tokens.axaml)
tests/                                  Core unit tests, Windows adapter tests, UI tests and screenshot checks
design/                                 Claude Design boards (reference only, never built)
```
- Core depends on nothing of the others; Windows and App depend on Core; App wires everything up.
- The site API is one typed client in Core (`ISiteClient`, `SiteClient`), tested against the contract's examples. Its results are values (`SiteResult`), never exceptions; tokens and device codes are `Secret`s that never print.

## The loop (board `states`)
```
Watching ─▶ game running (WoW process seen) ─▶ file changed ─▶ settling (unchanged for 5 s)
   ▲                                                                  │
   │                         hash already in ledger ─▶ skip ◀─────────┤
   │                                                                  ▼
   └──── report + notification ◀── uploaded ◀── uploading ◀───────────┘
                                       │
            offline: keep the file, retry (1 min, backing off) ─┘
            401 token revoked: Not connected, "Reconnect"
            rejected newer format: Not uploaded, retried at the next app start or after 24 h
            file or folder gone: Not watching, "Scan again / Choose folder"
```
- Settling: the file must be unchanged (size and write time) for 5 s before it's read; the read is retried if the game still holds it.
- An upload ledger per account records every finished upload by SHA-256 with its outcome (`ok`, `nothing_new`, `rejected` + reason); a file whose hash is in the ledger is never uploaded again, without a request. Bounded (last 300 hashes), versioned (rule 10). Why: several friends upload, the site merges out-of-sync files (newer data wins), so re-sending a file it already judged only costs requests and noise.
- Retrying (decided with Nico, UP-06): `newer_format` blocks the same file for 24 h and only within one run of the app, so it is tried again at the next start or a day later (the site has no "which format do you read" endpoint); `no_community_yet` likewise for 1 h. `wrong_file`, `damaged`, `not_your_file`, `other_community`, `too_large`, `empty_file`, unknown codes and "Skip this one" are final for that hash. `may_not_upload` (403) keeps the token and tries again hourly. Offline and 5xx: 1 min, doubling to 30 min; 429: `Retry-After`; 401: every account "Not connected" until "Reconnect". A retry re-reads the file on disk, so it always sends the newest.
- Reading: with sharing (the game may hold the file), retried 3 times a second apart; the bytes are hashed and sent, never parsed. "Upload now" skips the 5 s and the ledger; "Upload automatically" off keeps a settled file as "Ready to upload".
- Implementation: `UploadLoop` in Core with one worker per account (`AccountState`, ordered by urgency: the overall status is the highest), uploads one at a time; `UploadHistory` for the ledger and history. Windows: `FileWatcherFactory` (FileSystemWatcher; the loop also checks every 30 s), `AddonFileReader`, `GameProcesses` (`Wow.exe` inside the install's `_forever_`, checked every 10 s, only to show "Playing").
- Pause: no uploads until the pause ends; changes are noted and the newest file goes up after.
- Several accounts are watched independently.

## Runs to the game (UP-12, board `runs-flow`)
- `RunsSync` in Core fetches `GET runs` when WoW is seen starting (the 10 s game check), every 5 min while it runs, and right after each upload that reached the site (a newer last upload on any account), also with WoW closed. Nothing else while WoW is closed; a failed fetch waits for the next tick. One fetch at a time; the timer and cancellation are owned and stopped (rule 11).
- `RunsLua` turns the whole answer into `CF_RUNS = { ... }` as data only (Nico, 2026-10-28): keys as `["key"]`, strings quoted with `\\`, `\"` and control characters as `\ddd`, invariant numbers, booleans, `null` left out (`nil` in lists). New site fields reach the addon without an uploader update; the addon's validator (CF-45) drops what it doesn't know. Safety caps only (depth 12, strings 500 characters, 4 MB per file): the site's own maximum (50 runs, 40 answers each, 200 received ids) is about a fifth of the size cap and nothing is cut (`RunsLuaTests`, which also read the file back with a strict Lua data reader).
- `IRunsFiles` (Windows: `RunsFiles`) writes `<_forever_>\Interface\AddOns\CommunitiesForever\Data\Runs.lua` once per install (accounts in one install share it), only when the bytes differ from the file on disk (so the first fetch after an addon update, which ships the empty file, writes again), via `Runs.lua.tmp` and a move. No addon folder: "Install the addon"; no `Data` folder (an addon before CF-45): "Update the addon", nothing written; `Data` without Runs.lua: written, "Restart WoW to see them" (the game loads only files that existed at its start). Never the addon's saved file (rule 1).
- Nothing is stored: the last fetch lives in memory (`RunsStatus`), so settings and history don't change.

## Storage (per Windows user)
- `%LocalAppData%\CommunitiesForever.Upload\settings.json`: versioned settings, migration step per version (CLAUDE.md rule 10). Version 1 (UP-05): `version`, `accounts` (`installPath` = the `_forever_` folder, `account`), `uploadAutomatically`, `startWithWindows`, `closeHidesToTray`, `notifications` (`EveryUpload`, `OnlyProblems`, `Off`), `siteUrl` (null for wow.vestras.nl). Version 2 (UP-10) adds `installUpdatesWhenWowClosed` (the 1 → 2 step sets it on). Written via a temporary file; a damaged file is kept as `settings.json.bad` and setup runs again; a file from a newer version is never overwritten. `SettingsStoreTests` keeps the released v1 text loading.
- Finding WoW (UP-05): `GameScanner` in Core over `IFileSystem` (folders and file times only, no way to read contents) and `IInstallHints` (Windows: Blizzard registry keys, Battle.net.config `DefaultInstallPath`, the usual folders on every fixed drive).
- `updates.json` (version 1, UP-10): the version handed to the installer, "Updated to …" until it is closed, the last check. An unreadable or newer file starts afresh.
- `history.json` (version 1, UP-06): per account (`installPath`, `account`) the `ledger` (last 300: `hash`, `outcome`, `rejection`, `at`) and the `history` (last 50 uploads: the site's id, time, status, rejection, schema version, summary line, highlights, report link). Same migration rules as the settings; an unreadable file starts afresh (at worst one file goes up again). `UploadHistoryTests` keeps the released v1 text loading.
- The token: DPAPI-protected file next to it, never in settings.
- `logs\`: `uploader.log` rolled at 1 MB, at most 7 files (`RollingFileLoggerProvider`). No tokens, no highlights or other people's names (tested).

## Site API
Defined by the web repo in `docs/UPLOADER-API.md`: connecting with the OAuth 2.0 device authorization flow (RFC 8628: device code, then token polling), uploading with the Bearer token (RFC 6750 error responses), identity, disconnecting by token revocation (RFC 7009), update feed. The app follows that document for every endpoint, field and error code. The upload response carries the summary the app shows ("5 levels gained", "Ves 37 → 39") and the status (`ok`, `nothing_new`, `rejected` with a reason such as `newer_format`).

## Packaging and updates
- Velopack builds a per-user installer and update packages on a `v*` tag on `main` (UP-10): self-contained (no .NET to install, no admin prompt; decided with Nico), pack id `CommunitiesForever.Uploader` (its folder `%LocalAppData%\CommunitiesForever.Uploader` never collides with the data folder), no Velopack shortcuts (the app keeps its own, which carries the notification id).
- The workflow owns the feed, the Velopack way (UP-13, web WEB-35b): before `vpk pack` it pulls the site's `releases.win.json` and the newest full package (`GET /api/uploader/v1/releases/<file>` with the deploy secret; `vpk download http` can't send it), trims the feed to the 2 newest earlier versions and checks the package's SHA256. `vpk pack` then makes a delta package and the complete feed, which is signed and published; the site keeps the feed byte for byte and exactly the packages it lists (3 versions). The first release finds nothing (404) and packs from scratch: full package only.
- The feed is signed: the release workflow signs `releases.win.json` with an ECDSA P-256 key (secret in the `release` environment) and publishes `releases.win.json.sig` (the site serves both unchanged, WEB-35b); the app checks it with the public key in `FeedKey` before Velopack reads the feed. The feed names each package's size and SHA256, which Velopack checks, so the signature covers the packages too.
- `UpdateChecker` (Core): at start and every 24 hours with the token; downloads quietly; installs on quit, at the next start, or (setting "Install updates when WoW is closed", on by default) with a quiet restart when no game runs and nothing waits to upload; never during an upload ("Restart now" waits for it). Feed unreachable: again in an hour. A bad signature or download: thrown away, fetched again at the next check. An install that didn't take shows "Update failed" (`updates.json` remembers the version handed over). A required update (`minimumVersion` from `GET updates/status`) holds every upload (`IUploadLoop.Hold`) until it is in; the only loud update case (banner, tray dot). Boards `updates-flow`, `settings-updates`, `update-states`.
- Uninstall (Velopack's before-uninstall hook, `Uninstall.CleanUp`): removes the Run entry, the notification sender and link scheme, the Start menu shortcut and the token; keeps settings, history and logs for a reinstall.
- The installer and the update feed are hosted by the site (repos are private), at `/uploader/download` and the feed path in `UPLOADER-API.md`; the app reads the feed with its upload token.
- Not code-signed (decided with Nico): Windows SmartScreen warns once on install; Smart App Control (Windows 11, fresh installs) blocks unsigned apps outright, and friends turn it off (README "Installing", decided with Nico for UP-10).
