# AUTOFIXDIM / ASD Agent Rules

High-priority rules for AI coding agents. Keep this file short.

- **Hard constraints** stay here.
- **Paths, toolchain, build/deploy, command surface** → `docs/Environment.md`, `docs/Deployment.md`.
- **Recognition / dimension / regression detail** → `docs/RecognitionRules.md`, `docs/DimensionRules.md`, `docs/DimensionLayoutRegression.md`.
- **Session continuity** → dated `HANDOFF-*.md` (ephemeral; stable conclusions belong in `docs/`).

## Safety

- Do not batch-delete files or directories.
- Do not use `del /s`, `rd /s`, `rmdir /s`, `Remove-Item -Recurse`, or `rm -rf`.
- If deletion is required, delete only one explicit file path at a time.
- If bulk deletion seems necessary, stop and ask the user to handle it manually.
- Do not revert or overwrite user changes unless the user explicitly requests it.

## Development Workflow

- Do not proactively compile, run tests, launch AutoCAD, or run CAD scripts.
- Only run build, deployment, `cad-test`, AutoCAD, or `ASD` validation when the user explicitly asks.
- Do not edit source code during documentation-only tasks.
- Before changing behavior, read the relevant `docs/` pages and inspect the current source.
- Prefer small, targeted changes that match existing code style.
- **Versioned DLL build (required when the user asks to compile a loadable / test DLL):**
  - Output under `bin\Debug-vN\` (example: `bin\Debug-v1\AutoFixtureDim.dll`), not a bare overwrite of `bin\Debug\` as the deliverable.
  - Before each versioned build, inspect `bin\` for existing `Debug-v*` folders, take the highest `N`, and use **`N+1`**. Never reuse an earlier `-vN` folder or overwrite a previous versioned output.
  - In this worktree the versioned sequence starts at **`-v1`** and must strictly increment thereafter (`-v2`, `-v3`, …).
  - Keep related outputs of that build together in the same `Debug-vN` folder (`AutoFixtureDim.dll`, `CadAuto.Core.dll`, `CadAuto.CadAdapter.dll`).
  - See also `docs/Deployment.md` for regression / CAD deploy notes; the increment rule above is mandatory for agents.

## Product Invariants

- Generated annotation entities must be marked with XData app name `AUTOFIXDIM`.
- Cleanup commands must remove only plugin-generated objects marked with `AUTOFIXDIM` XData.
- Never create a `CENTER` layer.
- Never generate hole centerlines or cross centerlines.
- Hole recognition is based on the current user selection/window selection, not a full model-space scan.
- Dimension and leader attachment points must land on real geometry: edges, arcs, real endpoints, or real intersections.
- Do not attach annotations to theoretical intersections, virtual sharp corners, centerlines, auxiliary lines, dimension lines, projected points, or floating points.
- Use AutoCAD diameter control text `%%c`, not Unicode diameter symbols, for diameter callouts.
- Zero-length dimensions must never be emitted.

## Configuration And Layout Invariants

- Keep machining constants and tolerance text centralized in `RuleConfig.cs` when practical.
- Any `HoleKind.Pin` uses `PinHoleFitToleranceText`.
- Same-group pin spacing uses `PinCenterDistanceToleranceText`.
- Pin-group transfer uses `PinGroupDistanceToleranceText`.
- Functional-hole dimensions stay with their owning pin group and prefer a nearby valid local boundary.
- Loose/scatter hole-location chains may prefer a nearby valid local boundary when it shortens extension lines; the dimension line must stay outside the real contour interior and must not be sent to a distant global side merely to reduce crowding.
- Pin-group dimensions may use local boundary placement, but the dimension line must stay outside the real outer contour.

## Documentation Discipline

- Do not update project documentation unless the user asks for documentation work.
- Use exact dates in documentation; do not write relative time words such as "today", "yesterday", or "recently".
- Put durable agent instructions here; put evolving implementation knowledge under `docs/`.

## Docs Map

| Topic | File |
| --- | --- |
| Paths, runtime, source map, commands | `docs/Environment.md` |
| Build, versioned DLL, CAD smoke/regression deploy | `docs/Deployment.md` |
| Feature recognition | `docs/RecognitionRules.md` |
| Dimension planning rules | `docs/DimensionRules.md` |
| Four-side layout regression | `docs/DimensionLayoutRegression.md` |

## graphify

This repo has a knowledge graph under `graphify-out/` (query artifacts: `graph.json`, `GRAPH_REPORT.md`, labels, manifest).

- When the user types `/graphify`, use the installed graphify skill or instructions before doing anything else.
- For codebase questions, first run `graphify query "<question>"` when `graphify-out/graph.json` exists. Use `graphify path "<A>" "<B>"` and `graphify explain "<concept>"` for focused traversal. Prefer these over full `GRAPH_REPORT.md` or raw greps.
- Dirty local graphify files after hooks/updates are expected; do not skip graphify for that reason. Skip only if the task is about stale/wrong graph output, or the user says not to use it.
- If `graphify-out/wiki/index.md` exists, use it for broad navigation.
- Read `graphify-out/GRAPH_REPORT.md` only for broad architecture review or when query/path/explain are insufficient.
- After modifying code, run `graphify update .` to refresh the graph (AST-only, no API cost).
