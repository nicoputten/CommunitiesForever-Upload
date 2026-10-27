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

The app opens its window and a tray icon (right-click: Open, Quit). In Debug builds, `--gallery` opens the component gallery instead (every base component in every state):

```bash
dotnet run --project src/CommunitiesForever.Upload.App -- --gallery
```

## The site
The app talks to `https://wow.vestras.nl` through the uploader API (`docs/contract/UPLOADER-API.md`, a copy of the web repo's contract). Debug builds can use the web dev server instead:

```bash
dotnet run --project src/CommunitiesForever.Upload.App -- --site http://localhost:3000
```

When the web repo changes the contract, refresh the copy and run the tests:

```bash
powershell -File scripts/refresh-api-contract.ps1
```

## Design checks
`tests/…App.DesignChecks` renders every screen headless with Skia and compares it with `Baselines/*.png` (a pixel may differ by 8 of 255 per channel, at most 0.1% of the pixels may differ). On a difference the actual, expected and diff images land in `TestResults/design-checks/`; CI uploads them as the `design-checks` artifact. Baselines are made on CI (windows-latest) and committed only after Nico approved the look. For local iteration only:

```bash
UPDATE_BASELINES=1 dotnet test tests/CommunitiesForever.Upload.App.DesignChecks
```

App.Tests also checks that no `.axaml` or App code uses raw colours, sizes or durations (`TokenLintTests`) and that text tokens meet WCAG AA contrast (`ContrastTests`).

The design tokens mirror the web's `tokens.css`. When the web changes them, refresh the copy and run the tests:

```bash
powershell -File scripts/refresh-tokens.ps1
```

## Projects
| Project | What |
|---|---|
| `src/CommunitiesForever.Upload.Core` | All logic, UI-free and OS-free (`net10.0`) |
| `src/CommunitiesForever.Upload.Windows` | Thin Windows adapters behind Core's interfaces |
| `src/CommunitiesForever.Upload.App` | Avalonia app, composition root (DI) |
| `tests/…Core.Tests` | Core unit tests (site client against the contract's examples, connect flow) and the architecture test |
| `tests/…Windows.Tests` | Windows adapter tests (DPAPI token store) |
| `tests/…App.Tests` | Headless UI tests, token drift, token lint, contrast |
| `tests/…App.DesignChecks` | Screenshot design checks against `Baselines/` |

Package versions live in `Directory.Packages.props`, shared build settings (nullable, warnings as errors, analyzers) in `Directory.Build.props`, style in `.editorconfig`. CI (`.github/workflows/ci.yml`) runs restore, build, format check and tests on `windows-latest`.
