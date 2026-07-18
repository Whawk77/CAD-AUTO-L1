# AUTOFIXDIM / ASD Agent Rules

This file contains only high-priority rules that future AI coding agents must follow.
Detailed project knowledge lives in `docs/`.

## Safety

- Do not batch-delete files or directories.
- Do not use `del /s`, `rd /s`, `rmdir /s`, `Remove-Item -Recurse`, or `rm -rf`.
- If deletion is required, delete only one explicit file path at a time.
- If bulk deletion seems necessary, stop and ask the user to handle it manually.
- Do not revert or overwrite user changes unless the user explicitly requests it.

## Development Workflow

- Do not proactively compile, run tests, launch AutoCAD, or run CAD scripts.
- Only run build, deployment, `cad-test`, AutoCAD, or `ASD` validation commands when the user explicitly asks.
- Do not edit source code while doing documentation-only tasks.
- Before changing behavior, read the relevant docs under `docs/` and inspect the current source.
- Prefer small, targeted changes that match the existing code style.
- Store each newly built test DLL set in a new versioned output folder whose name ends in `-vNNN`, starting with `-v190`; increment the suffix for every subsequent build (`-v191`, `-v192`, and so on) and never reuse an earlier build folder.

## Active Project

- Active source: `D:\work\AI\project\autocad-dim`
- Main project file: `AutoFixtureDim.csproj`
- Target product: AutoCAD 2020 .NET Framework plugin for fixture-part annotation.
- Main command: `ASD`.
- Current compatibility/helper commands in source: `AUTOFIXDIM`, `ASD2`, `ASD3`, `ASD4`.

## Product Invariants

- Generated annotation entities must be marked with XData app name `AUTOFIXDIM`.
- Cleanup commands must remove only plugin-generated objects marked with `AUTOFIXDIM` XData.
- Never create a `CENTER` layer.
- Never generate hole centerlines or cross centerlines.
- Hole recognition is based on the current user selection/window selection, not a full model-space scan.
- Dimension and leader attachment points must land on real geometry: edges, arcs, real endpoints, or real intersections.
- Do not attach annotations to theoretical intersections, virtual sharp corners, centerlines, auxiliary lines, dimension lines, projected points, or floating points.
- Use AutoCAD diameter control text `%%c`, not Unicode diameter symbols, for diameter callouts.

## Configuration And Rules

- Keep machining constants and tolerance text centralized in `RuleConfig.cs` when practical.
- Preserve pin-hole fit behavior: any `HoleKind.Pin` uses `PinHoleFitToleranceText`.
- Preserve same-group pin spacing tolerance from `PinCenterDistanceToleranceText`.
- Preserve pin-group transfer tolerance from `PinGroupDistanceToleranceText`.
- Functional-hole dimensions stay with their owning pin group and prefer a nearby valid local boundary.
- Loose/scatter hole-location chains may use a nearby valid local boundary when it shortens extension lines; the final dimension line must remain outside the real contour interior and must not be sent to a distant global side merely to reduce crowding.
- Pin-group dimensions may use local boundary placement, but the dimension line must stay outside the real outer contour.
- Zero-length dimensions must never be emitted.

## Documentation

- Do not update project documentation automatically unless the user asks for documentation work.
- Use exact dates in documentation; do not write relative time words such as "today", "yesterday", or "recently".
- Put durable AI instructions here.
- Put implementation details and evolving project knowledge in:
  - `docs/Environment.md`
  - `docs/Deployment.md`
  - `docs/RecognitionRules.md`
  - `docs/DimensionRules.md`
