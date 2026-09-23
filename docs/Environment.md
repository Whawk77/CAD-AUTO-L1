# Environment

## Project Paths

- Workspace verified on 2026-09-10: `D:\work\AI\9.1 托压块版本`
- Git directory: `D:\work\AI\9.1 托压块版本\.git`. Recheck `Get-Location` and `git rev-parse --show-toplevel` after switching workspaces.
- Remote: `https://github.com/Whawk77/CAD-AUTO-L1.git`
- Main project file: `AutoFixtureDim.csproj` (repo root)
- Active product sources in this repo:
  - `CadAuto.Core/` — planning, recognition, rules, geometry
  - `CadAuto.CadAdapter/` — AutoCAD rendering and adapters
  - `CadAuto.Core.Tests/` — core unit/plan tests
  - Root plugin entry: `Commands.cs`, `PluginEntry.cs`, …
- Default build output (may be locked by AutoCAD): `bin\Debug\AutoFixtureDim.dll`
- Loadable/test DLL deliverables must use a fresh `bin\Debug-vN\` folder containing all three plugin DLLs; see `docs/Deployment.md`.

## Runtime

- Target host: AutoCAD 2020
- AutoCAD path: `D:\Program Files\Autodesk\AutoCAD 2020`
- Target framework: .NET Framework 4.7.2
- Platform target: x64

## Build Command

Use this only when the user explicitly asks to compile or test. Run from the verified repository root; choose the next unused version before each build:

```powershell
$versions = @(Get-ChildItem -LiteralPath .\bin -Directory -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match '^Debug-v[0-9]+$' } |
    ForEach-Object { [int]($_.Name -replace '^Debug-v', '') })
$nextVersion = 1 + [int](($versions | Measure-Object -Maximum).Maximum)
dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug "/p:OutputPath=bin\Debug-v$nextVersion\\" /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal
```

Notes:

- Keep `/p:PostBuildEvent=` unless the user explicitly wants the project post-build copy to run.
- Use `DebugSymbols=false` and `DebugType=None` when AutoCAD may lock `bin\Debug\AutoFixtureDim.pdb`.
- Start at `Debug-v1` if none exists; otherwise use the highest existing version plus one. Never reuse a previous output directory. Keep `AutoFixtureDim.dll`, `CadAuto.Core.dll`, and `CadAuto.CadAdapter.dll` together; Core-test output stays separate.

## Source Map

- `Commands.cs`: AutoCAD command entrypoints and top-level workflow.
- `CadAuto.Core` planning (`DimensionPlanner`, …): dimension plan generation, pin groups, functional/loose holes, structure dims, suppression.
- `CadAuto.CadAdapter/Recognition/FeatureRecognizer.cs`: the only production recognizer (outline, hole, pin, thread, slot, chamfer, fillet). `CadAuto.Core/Recognition/FeatureRecognizer2D.cs` is used exclusively by `CadAuto.Core.Tests`; the two implementations are not yet unified.
- `CadAuto.CadAdapter` rendering (`DimensionDrawer`, …): linear dimensions, stacking, local boundary, leaders, debug labels.
- `CadAuto.Core/Rules/DimensionRuleConfig.cs`: machining constants, tolerance text, number formatting, and thread minor-diameter mapping.
- Dim style / layer / XData helpers: style selection, annotation layer, `AUTOFIXDIM` marking and cleanup.

## Current Command Surface

- `ASD`: generate annotations.
- `AUTOFIXDIM`: compatibility alias for normal generation.
- `ASD4`: diagnostic generation with side selection.
- `ASD5`: generate outline-related annotations only.
- `ASD6`: generate hole-related annotations only.
- `ASD7`: generate corner-feature annotations only.
- `ASDCOREDBG`: core diagnostic generation.
- `ASD3`: clear the most recently generated plugin annotation group.
- `AG1`: special line-processing helper.

Historical command names such as `AUTOFIXDIMREGEN`, `AUTOFIXDIMCLEAR`, and `ASDDBG` should not be documented as current source behavior unless they are reintroduced in code.
