# Backlog

Open stories only, in build order. Done stories move to `docs/STORIES-COMPLETED.md`.
Each story: criteria are testable. "Manual" items are steps only Nico can do; they go in the PR.

Build order: UP-01 to UP-14 are done. All of it is v1 (decided with Nico).
The site side is built in the web repo: WEB-33 (uploader API, merged), WEB-34 (uploader page), WEB-35 (installer hosting and update feed).

## Every UI story
- Build the screen with the tokens from `Tokens.axaml` and the view models; the board in `design/boards/` is a **visual reference only**: match spacing, type and colours, never copy its markup.
- Always the design system (CLAUDE.md rules 6 and 7); a deviation only after Nico said so, recorded in `docs/DESIGN-NOTES.md`.
- Keyboard use, focus states, screen reader names (Avalonia automation properties), contrast as in the web DESIGN.md.
- Window at the board's size (900 × 620) and resizable down to a sensible minimum.
- The screen gets its design checks (UP-03): headless screenshot baselines and key style checks.

## E6 Clean-up

### UP-15 Clean-up before the Doomsday release
Agreed with Nico (2026-10-29): the whole ecosystem (addon, web, bot, uploader) gets a clean-up before the Doomsday release. One or more PRs, branches `feature/up-15-<slug>`.
- Dependencies: the open Dependabot PRs (actions/checkout 7, actions/setup-dotnet 6, actions/upload-artifact 7) handled, and other outdated NuGet packages and GitHub Actions brought up to date. A major bump (Avalonia, Velopack, .NET) only when build and tests pass, and a risky one only after Nico's OK. A Dependabot PR that needs changes is redone on our own branch and the original closed with a pointer.
- Technical debt: a list of refactors, each with effort and risk, goes to Nico first; only what he picks is built. UP-14's memory behaviour stays (software rendering, the compacting GC after hiding).
- Docs: finished stories in `docs/STORIES-COMPLETED.md`; `README.md` and `docs/ARCHITECTURE.md` describe the app as it is; `CHANGELOG.md` `## Unreleased` reads well as release notes.
- Leftovers: dead code and leftovers removed, flaky tests fixed.
- `dotnet build`, `dotnet test` and `dotnet format --verify-no-changes` pass; screenshot baselines unchanged (or a change approved by Nico).

