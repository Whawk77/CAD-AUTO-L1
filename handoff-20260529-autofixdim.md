# Handoff: AUTOFIXDIM / ASD Debug Labels

Date: 2026-05-29

## Context

Active source:
`D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`

Primary project handoff:
`D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\PROJECT_HANDOFF.md`

Recommended style for next session: use `caveman` mode if available. User prefers short, direct responses.

## Current State

Modified files:
- `Commands.cs`
- `DimensionDrawer.cs`
- `PROJECT_HANDOFF.md`

Git status at handoff:
- `master...origin/master [ahead 3]`
- working tree has the three modified files above

Latest compiled test DLL:
`D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB60.dll`

Last verified build command:
`dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`

## Changes Made

### Command names

`Commands.cs` now registers:
- `ASD`: normal generate
- `AUTOFIXDIM`: compatibility normal generate
- `ASD2`: clear old plugin annotations, then regenerate
- `ASD3`: clear plugin annotations
- `ASDDBG`: clear old plugin annotations, regenerate, and add diagnostic labels

Old command registrations `AUTOFIXDIMREGEN` and `AUTOFIXDIMCLEAR` were removed.

### Pin-group local boundary rule

`DimensionDrawer.DeferredDim` has `PreferLocalBoundary`.

Local-boundary layout applies to:
- `PinDistance`
- `PinGroupDistance`
- pin-group functional-hole dimensions marked with `PreferLocalBoundary`

Loose/scatter dimensions do not get this flag and stay global-boundary unless a future rule changes that.

### Visual diagnostic labels

`ASDDBG` adds compact diagnostic `MText` beside all linear dimensions.

Labels are marked with `AUTOFIXDIM` XData, so `ASD3` clears them.

Label format:
`Owner|Role|Boundary|Side`

Examples:
- `PG3|FH1|LB|L`: pin group 3, functional-hole dimension 1, local boundary, left side
- `GEN|OW1|GB|B`: general overall-width dimension 1, global boundary, bottom side
- `GEN|N1|GB|L`: general normal dimension 1, global boundary, left side
- `L12|LH1|GB|R`: loose chain 12, loose-hole dimension 1, global boundary, right side

Role codes:
- `PD`: same-pin-group pin spacing
- `GD`: pin-group-to-pin-group transfer
- `FH`: functional hole attached to pin group
- `LH`: loose/scatter hole chain
- `DX` / `DY`: datum-hole X/Y location
- `OW` / `OH`: overall width/height
- `HL`: generic hole location
- `N`: normal structure/step dimension
- `BSW` / `TSW`: bottom/top structure width
- `LSH` / `RSH`: left/right structure height
- `SC`: slot center distance
- `SCH` / `SCV`: slot horizontal/vertical chain
- `SDH` / `SDV`: slot datum horizontal/vertical location
- `SPR`: slot located from nearest pin reference
- `SAS`: single-arc slot datum dimension

Boundary codes:
- `LB`: dimension line actually used a local boundary
- `GB`: global MainOutline envelope layout

Side codes:
- `B`: Bottom
- `T`: Top
- `L`: Left
- `R`: Right

Sequence numbers are per side/per owner/per role/per boundary key. Bottom/Top labels are numbered left to right; Left/Right labels are numbered top to bottom. Example: `GEN|N1|GB|L`, `GEN|N2|GB|L`.

## Important User Intent

User wants CAD-visible diagnostic text to be compact, but copied text must be clear enough for the next agent to identify the dimension meaning.

User considers pin-group attached functional-hole dimensions part of the pin group. Example: lower-left `45` dimensions in the screenshot should be treated as pin-group dimensions, not loose/scatter dimensions.

Any pin-group dimension should start layout from a nearby local boundary when possible. True loose/scatter dimensions should not.

## Next Steps

1. In AutoCAD, `NETLOAD`:
   `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB60.dll`
2. Run `ASDDBG` on the user's test drawing.
3. Ask user to copy or screenshot labels for any wrong dimension.
4. If a dimension shows `GEN|N...` but should be pin-group related, fix ownership/classification before changing layout.
5. If a pin-group label shows `GB` but should use local boundary, inspect `PreferLocalBoundary` propagation and `TryGetDimensionLocalBoundary`.
6. If labels clutter too much, reduce diagnostic label height or make only selected categories visible in `ASDDBG`.

## Caution

Do not batch-delete files or directories.

Do not use recursive delete commands.

Do not change XData app name `AUTOFIXDIM`; `ASD3` depends on it for cleanup.

Do not update `AGENTS.md` unless user explicitly asks or a rule must persist for future agents.
