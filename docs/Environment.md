# Environment

## Project Paths

- Workspace: `D:\work\AI\project\L1`
- Active source: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`
- Project file: `AutoFixtureDim.csproj`
- Build output: `bin\Debug\AutoFixtureDim.dll`

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

- `Commands.cs`: AutoCAD command entrypoints and top-level workflow.
- `GeometryCollector.cs`: selection prompts, datum prompts, and source object collection.
- `FeatureRecognizer.cs`: outline, hole, pin, thread, slot, chamfer, and fillet recognition.
- `DimensionDrawer.cs`: linear dimensions, pin groups, loose-hole chains, stacking, suppression, local boundary placement, debug labels, and interactive leaders.
- `RuleConfig.cs`: machining constants, tolerance text, number formatting, and thread minor-diameter mapping.
- `DimStyleManager.cs`: dimension style selection.
- `LayerManager.cs`: annotation layer selection.
- `AnnotationMetadata.cs`: `AUTOFIXDIM` XData marking and cleanup.

## Current Command Surface

- `ASD`: generate annotations.
- `AUTOFIXDIM`: compatibility alias for normal generation.
- `ASD2`: clear generated annotations, then regenerate.
- `ASD3`: clear generated plugin annotations.
- `ASD4`: diagnostic generation with side selection.

Historical command names such as `AUTOFIXDIMREGEN`, `AUTOFIXDIMCLEAR`, and `ASDDBG` should not be documented as current source behavior unless they are reintroduced in code.
