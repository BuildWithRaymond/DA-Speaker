# Development

## Requirements

- Windows x64
- .NET 10 SDK; `global.json` allows compatible stable .NET 10 feature bands
- Git

No game client is needed to build or run the automated tests.

## Build and test

```powershell
git clone https://github.com/BuildWithRaymond/DA-Speaker.git
cd DA-Speaker
dotnet restore DA-Speaker.slnx
dotnet build DA-Speaker.slnx -c Release --no-restore
dotnet test DA-Speaker.slnx -c Release --no-build
```

The test suite covers script parsing, playback timing, pause/resume/stop, recovery,
clipboard cleanup, Windows messages, saved settings, hotkeys, and real form
controls with fake input. Native probes use private test windows. UI tests create
screenshots under `artifacts/design/`.

## Run

```powershell
dotnet run --project src/DA-Speaker -c Release
```

The application requests administrator rights when launched. For an actual game
check, start the compiled executable and accept the Windows elevation prompt.

Use **Send test** in the main window to check input, and **Settings →
Troubleshooting** to view or save the activity log. All input uses the verified
direct Ctrl+V path.

## Publish

```powershell
dotnet publish src/DA-Speaker/DA-Speaker.csproj -c Release -r win-x64 -p:SelfContained=false -p:PublishSelfContained=false -p:PublishSingleFile=true -p:DebugType=None -p:DebugSymbols=false -o artifacts/release
```

The result requires the .NET 10 Desktop Runtime x64. Include `LICENSE`,
`THIRD_PARTY_NOTICES.md`, and a short user guide with distributed builds.

## Project layout

| Path | Purpose |
| --- | --- |
| `src/DA-Speaker/` | Windows Forms app, parsing, playback, settings, and input |
| `tests/DA-Speaker.Tests/` | Unit tests and Windows UI/native checks |
| `docs/` | Usage, input design, and showcase images |
| `examples/` | Ready-to-edit scripts |
| `tools/` | Optional visual asset generation |

The app icon is already checked in. Regenerating it with `tools/make-icon.py`
requires Python and Pillow; neither is required by the app or the normal build.

Repository showcase images are rendered from the checked-in application captures:

```powershell
powershell -NoProfile -File tools/make-showcase.ps1
```

## Input behavior

Keep the parser, playback runner, and Windows input layer separate. Review
[input design](input-design.md) before changing dispatch or clipboard handling.
Successful dispatch is not a game-side receipt. Record live-client observations
separately from test results.
