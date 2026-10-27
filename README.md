# CommunitiesForever Upload

Windows app that uploads the CommunitiesForever addon's data to the companion site after every World of Warcraft: Forever session. See `CLAUDE.md`, `docs/ARCHITECTURE.md` and `docs/STORIES.md`.

## Local setup
1. Windows 10 or 11 with the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) (Visual Studio 2026 with the ".NET desktop development" workload installs it). `global.json` pins the 10.0.1xx feature band or newer. Windows 11 **Smart App Control** must be off: it blocks the unsigned Avalonia analyzer and the test programs this repo builds.
2. Clone, then from the repo root:

```bash
dotnet build
```

```bash
dotnet test
```

```bash
dotnet run --project src/CommunitiesForever.Upload.App
```

The app opens its window and a tray icon (right-click: Open, Quit).

## Projects
| Project | What |
|---|---|
| `src/CommunitiesForever.Upload.Core` | All logic, UI-free and OS-free (`net10.0`) |
| `src/CommunitiesForever.Upload.Windows` | Thin Windows adapters behind Core's interfaces |
| `src/CommunitiesForever.Upload.App` | Avalonia app, composition root (DI) |
| `tests/…Core.Tests` | Core unit tests and the architecture test |
| `tests/…App.Tests` | Headless UI tests |

Package versions live in `Directory.Packages.props`, shared build settings (nullable, warnings as errors, analyzers) in `Directory.Build.props`, style in `.editorconfig`. CI (`.github/workflows/ci.yml`) runs restore, build, format check and tests on `windows-latest`.
