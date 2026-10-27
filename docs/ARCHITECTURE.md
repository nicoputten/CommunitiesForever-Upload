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
| UI | Avalonia 12 (MVVM with CommunityToolkit.Mvvm), compiled bindings | styleable to the shared tokens, headless rendering for screenshot tests |
| Tray icon | Avalonia `TrayIcon` | built in |
| Notifications | Windows toast notifications (UP-08 picks the package, ask first) | |
| HTTP | `HttpClient` | |
| Dependency injection | Microsoft.Extensions.DependencyInjection, container built only in App | interfaces everywhere, Core and Windows each offer one `AddCore()` / `AddWindowsAdapters()` |
| Logging | Microsoft.Extensions.Logging (Core sees `ILogger` only), small rolling file sink of our own in Windows | no Serilog; a test checks no token reaches the log |
| Time | `TimeProvider` (BCL) for every timer in Core; `FakeTimeProvider` (Microsoft.Extensions.TimeProvider.Testing) in tests | settle, retry and pause tests run instantly |
| JSON | System.Text.Json with source generation | no package |
| Token storage | DPAPI (`System.Security.Cryptography.ProtectedData`, current user) | CLAUDE.md rule 4 |
| Packaging and updates | Velopack | per-user install without admin rights, delta updates, feed served by the site (WEB-35) |
| Tests | xUnit v3, Avalonia.Headless for UI and screenshots, hand-written fakes, plain `Assert` | no mocking or assertion library |
| CI | GitHub Actions on `windows-latest` | |

Every package at an exact version. Anything not listed needs Nico's OK (CLAUDE.md rule 9). UP-01 checks the current versions.

### Techniques (agreed with Nico, 2026-10-27)
- No generic Host: plain DI plus one class of our own that starts and stops the loop; settings stay in our versioned `settings.json`.
- Retry and backoff are our own small state machine in Core, not Polly.
- File watching: `FileSystemWatcher` as the trigger plus a 30 s poll as the safety net; both feed the same settle step.
- Fake site: a fake `HttpMessageHandler` in unit tests, and `tools/FakeSite` (ASP.NET Core minimal API, no package) for trying pairing and uploads by hand, until WEB-33 is live.
- Windows bits without packages: named mutex + named pipe for single instance, HKCU `Run` key for autostart, the WinRT notification API through the `net10.0-windows10.0.19041.0` target framework for toasts (UP-08 confirms).
- Startup: ReadyToRun on publish; no Native AOT.
- Core targets plain `net10.0`, so Windows APIs are not even available there; `ArchitectureTests` in Core.Tests reads Core's metadata and fails on Avalonia, Windows APIs, the other projects, direct file system, process, registry or socket use and P/Invoke. `HttpClient` is allowed in Core but always injected.

## Long running (CLAUDE.md rule 11)
The app lives for weeks in the tray, so memory must stay flat:
- Whoever creates something with a lifetime disposes it: timers, watchers, `CancellationTokenSource`s, DI scopes, the tray icon. App disposes the service provider on exit.
- Every subscribed event handler is unsubscribed (or a weak event is used); no static events.
- One shared `HttpClient`; no caches or collections without a maximum (history 50 per account, a bounded queue, rolling logs).
- Tests: every view, view model and loop component that can be created more than once is checked to be collected after close or stop (`WeakReference` + GC); the loop gets a soak test in UP-06.

## Layering
```
src/CommunitiesForever.Upload.Core/     pure: state machine, settle timer, hashing, retry and queue, pairing flow,
                                        settings and history models with migrations, API contract types; no UI, no OS
src/CommunitiesForever.Upload.Windows/  thin adapters: WoW folder discovery (registry, Battle.net config), file watcher,
                                        autostart, DPAPI token store, toasts, single-instance
src/CommunitiesForever.Upload.App/      Avalonia app: views, view models, tray, styles (Tokens.axaml)
tests/                                  Core unit tests, Windows adapter tests, UI tests and screenshot checks
tools/FakeSite/                         fake site following UPLOADER-API.md, for local runs until WEB-33 is live (UP-04)
design/                                 Claude Design boards (reference only, never built)
```
- Core depends on nothing of the others; Windows and App depend on Core; App wires everything up.
- The site API is one typed client in Core (`ISiteApi`), tested against a fake server.

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
            rejected newer format: Not uploaded, retried after the next site update
            file or folder gone: Not watching, "Scan again / Choose folder"
```
- Settling: the file must be unchanged (size and write time) for 5 s before it's read; the read is retried if the game still holds it.
- An upload ledger per account records every finished upload by SHA-256 with its outcome (`ok`, `nothing_new`, `rejected` + reason); a file whose hash is in the ledger is never uploaded again, without a request. Bounded (last few hundred hashes), versioned (rule 10). Why: several friends upload, the site merges out-of-sync files (newer data wins), so re-sending a file it already judged only costs requests and noise. Whether `rejected: newer_format` is retried once after a site update is decided in UP-06.
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
- Open point for UP-10: Windows 11 **Smart App Control** blocks unsigned apps outright (no "Run anyway"). Found on 2026-10-27 when it blocked the unsigned Avalonia analyzer and test programs on Nico's PC. Friends with it on can't install an unsigned app, so UP-10 decides between code signing (e.g. Azure Trusted Signing) and an install guide that explains turning it off.
