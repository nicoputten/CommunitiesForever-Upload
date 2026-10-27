# Architecture

## Core rules
- The app is a courier: it finds the addon file, waits until the game has finished writing it, uploads the bytes and shows the site's answer. It never interprets the file (CLAUDE.md rule 1).
- One upload token per installed app, upload-only, created by browser pairing or pasted by hand, revocable on the site.
- It runs quietly: tray first, window on demand, notifications by preference.
- Everything works offline: a file that couldn't be sent is kept and sent later.

## Stack
| Part | Choice | Why |
|---|---|---|
| Runtime | .NET 10 (LTS), C# | Nico's choice |
| UI | Avalonia 11 (MVVM with CommunityToolkit.Mvvm) | styleable to the shared tokens, headless rendering for screenshot tests |
| Tray icon | Avalonia `TrayIcon` | built in |
| Notifications | Windows toast notifications (UP-08 picks the package, ask first) | |
| HTTP | `HttpClient` | |
| Token storage | DPAPI (`System.Security.Cryptography.ProtectedData`, current user) | CLAUDE.md rule 4 |
| Packaging and updates | Velopack | per-user install without admin rights, delta updates, feed served by the site (WEB-35) |
| Tests | xUnit, Avalonia.Headless for UI and screenshots | |
| CI | GitHub Actions on `windows-latest` | |

Every package at an exact version. Anything not listed needs Nico's OK (CLAUDE.md rule 9). UP-01 checks the current versions.

## Layering
```
src/CommunitiesForever.Upload.Core/     pure: state machine, settle timer, hashing, retry and queue, pairing flow,
                                        settings and history models with migrations, API contract types; no UI, no OS
src/CommunitiesForever.Upload.Windows/  thin adapters: WoW folder discovery (registry, Battle.net config), file watcher,
                                        autostart, DPAPI token store, toasts, single-instance
src/CommunitiesForever.Upload.App/      Avalonia app: views, view models, tray, styles (Tokens.axaml)
tests/                                  Core unit tests, Windows adapter tests, UI tests and screenshot checks
design/                                 Claude Design boards (reference only, never built)
```
- Core depends on nothing of the others; Windows and App depend on Core; App wires everything up.
- The site API is one typed client in Core (`ISiteApi`), tested against a fake server.

## The loop (board `states`)
```
Watching ─▶ game running (WoW process seen) ─▶ file changed ─▶ settling (unchanged for 5 s)
   ▲                                                                  │
   │                         same hash as last upload ─▶ skip ◀───────┤
   │                                                                  ▼
   └──── report + notification ◀── uploaded ◀── uploading ◀───────────┘
                                       │
            offline: keep the file, retry (1 min, backing off) ─┘
            401 token revoked: Not connected, "Reconnect"
            rejected newer format: Not uploaded, retried after the next site update
            file or folder gone: Not watching, "Scan again / Choose folder"
```
- Settling: the file must be unchanged (size and write time) for 5 s before it's read; the read is retried if the game still holds it.
- The hash (SHA-256) of the last uploaded file per account is kept, so an unchanged file is skipped without a request.
- Pause: no uploads until the pause ends; changes are noted and the newest file goes up after.
- Several accounts are watched independently.

## Storage (per Windows user)
- `%LocalAppData%\CommunitiesForever.Upload\settings.json`: versioned settings (watched folders, behaviour, notifications, site URL), migration step per version (CLAUDE.md rule 10).
- `history.json`: the last 50 uploads per account with the site's summary line and report link.
- The token: DPAPI-protected file next to it, never in settings.
- `logs\`: rolling log files (no tokens, no other people's BattleTags).

## Site API
Defined by the web repo in `docs/UPLOADER-API.md`: pairing (start, poll), token upload, "who am I", disconnect, update feed. The upload response carries the summary the app shows ("5 levels gained", "Ves 37 → 39") and the status (`ok`, `nothing_new`, `rejected` with a reason such as `newer_format`).

## Packaging and updates
- Velopack builds a per-user installer and update packages on a `v*` tag on `main` (UP-10).
- The installer and the update feed are hosted by the site (repos are private), at `/uploader/download` and the feed path in `UPLOADER-API.md`; the app reads the feed with its upload token.
- Not code-signed (decided with Nico): Windows SmartScreen warns once on install.
