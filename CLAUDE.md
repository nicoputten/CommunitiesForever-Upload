# CommunitiesForever Upload

A small Windows app for the friends who play **World of Warcraft: Forever** with the [CommunitiesForever](https://github.com/nicoputten/CommunitiesForever) addon. It watches the addon's SavedVariables file and uploads it to the companion site [CommunitiesForever-Web](https://github.com/nicoputten/CommunitiesForever-Web) (`wow.vestras.nl`) after every play session, so nobody has to remember. It sits in the tray, pairs with the site through the browser, and shows what each upload changed.

## Read first
- `docs/ARCHITECTURE.md`: core rules, stack, layering, the watch-and-upload loop, storage, packaging
- `docs/STORIES.md`: open stories with acceptance criteria, in build order (done ones: `docs/STORIES-COMPLETED.md`)
- `design/README.md` and `design/boards/`: the Claude Design boards for every screen (visual reference only)
- **The site's API contract:** `docs/UPLOADER-API.md` in the web repo (`D:\repos\CommunitiesForever-Web\docs\UPLOADER-API.md`, GitHub `nicoputten/CommunitiesForever-Web`). The web repo owns it; this app follows it. When they disagree, the web repo wins and this repo gets fixed.
- **The look:** the web repo's `docs/DESIGN.md` (tokens, type, data age) and `src/styles/tokens.css`. The app uses the same "Night over Azeroth" tokens.

## Platform facts
- Windows 10 and 11 only. WoW: Forever lives in the `_forever_` folder of the World of Warcraft install; the addon file is `WTF\Account\<ACCOUNT>\SavedVariables\CommunitiesForever.lua`. One PC can have several WoW accounts.
- The game writes the file when the player logs out, types `/reload` or quits, and may write it in several steps.
- The site is `https://wow.vestras.nl` (configurable for development, e.g. `http://localhost:3000`).

## Hard rules
1. **No parsing of the addon's data.** The app never reads the content of `CommunitiesForever.lua` beyond its bytes: no Lua parsing, no counting characters, no format checks. It hashes and uploads the file. Everything it shows about the content (counts, level ups, "newer format") comes from the site's answer. The data format has exactly two readers already (addon and web); a third is not allowed.
2. `src/CommunitiesForever.Upload.Core` is UI-free: no Avalonia, no Windows APIs, no file system or network access except through interfaces. All logic (state machine, settle timer, retry, pairing flow, history) lives here and is fully unit tested.
3. Windows specifics (registry, Battle.net config, autostart, toasts, tray) live behind interfaces in `src/CommunitiesForever.Upload.Windows` and stay thin, with no business logic.
4. **Secrets:** the upload token is stored only with Windows DPAPI (current user), never in plain text, logs, crash reports or settings files. Logs never contain tokens or BattleTags of others.
5. The app talks only to the configured site and only through the endpoints in `UPLOADER-API.md`. No telemetry, no other hosts (except what Velopack needs for updates, which is the same site).
6. **Always follow the design system.** Screens use the shared tokens (ported to `Tokens.axaml`) and match their board in `design/boards/`. No raw colours, font sizes, spacing, radii or durations outside the tokens. The boards are a visual reference only, never copied. Accessible: keyboard use, focus states, screen reader names, contrast as in the web DESIGN.md.
7. **Deviating from the design only when Nico says so.** Ask first; an approved deviation goes into `docs/DESIGN-NOTES.md` "Deviations" (what, why, date) in the same PR. A changed screenshot baseline is only committed after Nico approved the new look.
8. English strings, hardcoded. No localization system.
9. No dependencies beyond those listed in `docs/ARCHITECTURE.md` without asking. Every package at an exact version.
10. **Backwards compatible settings.** Settings and local history are versioned; every change keeps working with files from earlier releases (a migration step per version), because friends update on different days.
11. **No memory leaks.** The app runs for weeks in the tray. Everything with a lifetime is owned and disposed: unsubscribe every event handler you subscribe (or use weak events), dispose timers, watchers, `CancellationTokenSource`s and DI scopes, no static events or caches that grow without a bound, one shared `HttpClient`. Long-living collections (history, queue, logs) have a fixed maximum. Every view, view model and loop component that can be created more than once gets a test that it is collected after it is closed or stopped (`WeakReference` + GC), and the loop gets a soak test (UP-06).

12. **Nothing private, ever.** Treat everything committed as public forever, history included: code, docs, fixtures, screenshots, commit messages, PR texts. Even while the repo is private, so it can go public any time. Never commit secrets (tokens, keys, signing certificates, passwords, `.env` files, webhook URLs), real BattleTags, Discord IDs or usernames, real SavedVariables or uploads, names of real people beyond what Nico agreed to, IP addresses, NAS details or local paths with user names. Tests, docs and board screenshots use made-up values (`Name#1234`, `Ada#1001`). When in doubt, ask Nico before committing. If something private lands anyway, stop and tell Nico; rewriting history is his call.

## Commands
```bash
dotnet build                                    # build everything (warnings are errors)
dotnet test                                     # all tests: Core, architecture, headless UI
dotnet format --verify-no-changes               # style check, as in CI; drop the flag to fix
UPDATE_BASELINES=1 dotnet test tests/CommunitiesForever.Upload.App.DesignChecks   # rewrite screenshot baselines (local only; commit only Nico-approved CI images)
dotnet run --project src/CommunitiesForever.Upload.App   # start the app
```

## Branches
Gitflow: `feature/up-XX-<slug>` off `develop`, PRs into `develop`. `main` receives releases.

## Releases
- A tag `v*` on `main` builds the installer and the update package (Velopack) and publishes them to the site (UP-10, WEB-35). Pushing a tag ships to every friend's PC: only on Nico's explicit go-ahead per tag.
- Every user-facing change goes under `## Unreleased` in `CHANGELOG.md` in its own PR. When releasing, rename that heading to `## <tag> (<date>)`.

## Definition of done (per story)
- All acceptance criteria met
- `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass, CI green
- UI stories: the design checks (UP-03) for the screen, screenshots in the PR next to the board
- Manual steps only Nico can do (a real WoW install, a real upload, an install from the site) are written in the PR under "Manual"

## Working with Nico
- Experienced software developer. Be technical and concise.
- Ask clarifying questions before assuming anything ambiguous. He strongly prefers questions with a proposal each ("all OK" or by number) over rework.
- One story per branch and PR. Conventional commits.
- Usage is limited: keep sessions lean, don't re-read what you already know.
