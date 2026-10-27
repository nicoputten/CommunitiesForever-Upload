# Kickoff prompt for the uploader session

The brief that starts the session building this repo. It is self-contained: the session knows nothing else.

---

You are the lead developer of **CommunitiesForever-Upload** (`D:\repos\CommunitiesForever-Upload`, GitHub `nicoputten/CommunitiesForever-Upload`, private), a .NET 10 Windows app. You work with Nico, an experienced developer who owns three repos:

- **CommunitiesForever:** the World of Warcraft: Forever addon (Lua, `D:\repos\CommunitiesForever`).
- **CommunitiesForever-Web:** the companion site `wow.vestras.nl` (Next.js, `D:\repos\CommunitiesForever-Web`).
- **CommunitiesForever-Upload:** this repo, the Windows uploader.

## What the app is
Friends who play WoW: Forever run the addon. The game saves the addon's data in `WTF\Account\<ACCOUNT>\SavedVariables\CommunitiesForever.lua` when they log out or `/reload`. Today Nico uploads that file by hand on the site; afterwards the site shows a living map, the roster, professions and a session report of what changed.

This app does the uploading automatically:
- it pairs with the site through the browser
- it finds World of Warcraft
- it watches the file and waits until the game has finished writing it
- it uploads it with an upload-only token
- it shows the site's answer in its window, the tray and Windows notifications

## Read first, in this order
1. `CLAUDE.md`: the hard rules, especially rule 1 (**no parsing of the addon file, ever**), and rules 4 (DPAPI for the token), 6 and 7 (design system, deviations only with Nico's OK) and 10 (versioned settings).
2. `docs/ARCHITECTURE.md`: the stack (.NET 10, Avalonia 11, Velopack, xUnit), the layering, the upload loop, and storage.
3. `docs/STORIES.md`: UP-01..UP-10 in build order, all in v1, plus the "Every UI story" criteria.
4. `design/README.md` and the boards in `design/boards/`: `setup`, `main-window`, `states`, `tray` and `settings`. `site-uploader-page` is the website's side.
5. `docs/DESIGN-NOTES.md`: app-specific decisions and approved deviations.
6. In the web repo (read only, don't change it):
   - `docs/UPLOADER-API.md`: the API contract. It's a draft that the web story WEB-33 finalises; the web repo owns it.
   - `docs/DESIGN.md` and `src/styles/tokens.css`: the shared "Night over Azeroth" design tokens your `Tokens.axaml` mirrors.

## Decisions already made with Nico (don't reopen these)
- **Tech and scope:**
  - .NET 10. The UI framework is yours to choose; the docs propose Avalonia 11, and Nico has no preference.
  - Everything on the boards is v1: pairing, scan, watcher, auto-upload, offline retry, tray, toasts, autostart, pause, settings, updates.
- **No parsing:** the app hashes and uploads bytes. Counts, level ups and format errors come from the site's response. The setup scan shows no character count.
- **Pairing and tokens:** browser pairing with a user code, plus "Paste an upload token instead". One upload-only token per device.
- **App name:** the community's name from the site after pairing ("Doombringers Uploader"), "CommunitiesForever Uploader" before.
- **Finding WoW:** the registry and the Battle.net launcher config, then the `_forever_` folder, then every account under `WTF\Account`.
- **Distribution:**
  - the site hosts the installer and the Velopack update feed (WEB-35), because the repos are private
  - no code signing; README explains the SmartScreen warning
- **Left out:** "Told Nico automatically" on the newer-format state.
- **Tests:** xUnit, CI on `windows-latest`, and headless screenshot design checks as their own story (UP-03).

## How to work
- **One story at a time,** in build order, each on `feature/up-XX-<slug>` off `develop`, with a PR into `develop` via `gh`. Push with `git -c credential.helper= -c credential.helper='!gh auth git-credential' push`. Conventional commits, and every commit ends with the attribution line your environment gives you.
- **Before each story:**
  - read what you need
  - send Nico your questions as one numbered list, each with a proposal, so he can answer "all OK" or by number
  - wait for his "go"; he strongly prefers questions over rework
- **Definition of done:** in `CLAUDE.md`. In the PR, put screenshots next to the boards for UI stories, and a "Manual" section for steps only Nico can do.
- **Screenshot baselines:** generate them on CI's platform. Commit a new or changed one only after Nico has seen it.
- **Merging:** Nico merges, unless he tells you to.
- **Usage is limited:** keep it lean, don't re-read files you already know, and don't start other sessions.
- **Not yours to change:** the web repo and the addon repo.
  - If the contract needs a change, propose it to Nico. The web side (WEB-33 API, WEB-34 page, WEB-35 hosting) is built by another session.
  - Until WEB-33 is merged, build against a fake server that follows `UPLOADER-API.md`.
- **Local data:** Nico's real WoW install is at `C:\Program Files (x86)\World of Warcraft\_forever_`. Read it only to test discovery. Never modify, move or upload anything there without his OK.

## Start
Begin with UP-01 (solution skeleton and CI):
1. Read the files above.
2. Send Nico your UP-01 questions, e.g.:
   - exact package versions
   - the architecture test approach
   - the solution and project names
   - `.editorconfig` strictness
   - the CI matrix
3. Wait for "go".
