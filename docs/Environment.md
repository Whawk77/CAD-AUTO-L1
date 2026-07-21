# Environment

## Project Paths

- This worktree (current agent workspace): `D:\work\AI\project\L1-grok`
- Main git worktree / shared `.git`: `D:\work\AI\project\L1`
- Remote: `https://github.com/Whawk77/CAD-AUTO-L1.git`
- Main project file: `AutoFixtureDim.csproj` (repo root)
- Active product sources in this repo:
  - `CadAuto.Core/` — planning, recognition, rules, geometry
  - `CadAuto.CadAdapter/` — AutoCAD rendering and adapters
  - `CadAuto.Core.Tests/` — core unit/plan tests
  - Root plugin entry: `Commands.cs`, `PluginEntry.cs`, `RuleConfig.cs`, …
- Default build output (may be locked by AutoCAD): `bin\Debug\AutoFixtureDim.dll`
- Prefer a versioned output folder when AutoCAD holds locks; see `docs/Deployment.md`.

## Runtime

- Target host: AutoCAD 2020
- AutoCAD path: `D:\Program Files\Autodesk\AutoCAD 2020`
- Target framework: .NET Framework 4.7.2
- Platform target: x64

## Build Command

Use this only when the user explicitly asks to compile or test:

```powershell
dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal
```

Notes:

- Keep `/p:PostBuildEvent=` unless the user explicitly wants the project post-build copy to run.
- Use `DebugSymbols=false` and `DebugType=None` when AutoCAD may lock `bin\Debug\AutoFixtureDim.pdb`.
- AutoCAD can lock loaded DLLs; use a fresh versioned test DLL when overwrite fails.

## Source Map

- `Commands.cs` / `V177Source/AutoFixtureDim/`: AutoCAD command entrypoints and top-level workflow.
- `CadAuto.Core` planning (`DimensionPlanner`, …): dimension plan generation, pin groups, functional/loose holes, structure dims, suppression.
- `CadAuto.Core` recognition: outline, hole, pin, thread, slot, chamfer, and fillet recognition.
- `CadAuto.CadAdapter` rendering (`DimensionDrawer`, …): linear dimensions, stacking, local boundary, leaders, debug labels.
- `RuleConfig.cs`: machining constants, tolerance text, number formatting, and thread minor-diameter mapping.
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
