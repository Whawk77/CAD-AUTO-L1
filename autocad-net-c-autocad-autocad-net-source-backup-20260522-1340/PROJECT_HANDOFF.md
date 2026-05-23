# AUTOFIXDIM / ASD Handoff

Last synced: 2026-05-21

## Goal
AutoCAD 2020 .NET Framework plugin for semi-automatic fixture-part annotation. Active command is `ASD`; old commands remain for compatibility.

The plugin annotates:
- full envelope outline width and height
- selected step and body dimensions
- MainOutline chamfer and fillet callouts
- datum-hole based hole position dimensions
- pin-group and pin-to-pin distances with configured tolerance text
- diameter, pin-hole, and thread-hole callouts placed interactively

It never creates centerlines or a `CENTER` layer.

## Project
- Workspace: `D:\work\AI\project\autocad-net-c-autocad-autocad-net`
- Project file: `AutoFixtureDim.csproj`
- Target framework: .NET Framework 4.7.2
- AutoCAD install: `D:\Program Files\Autodesk\AutoCAD 2020`
- Build command: `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /v:minimal`
- Build output: `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\AutoFixtureDim.dll`
- Deployment folder: `D:\app\不加班的小刘_工具箱\dll`
- Deploy script: `.\deploy_next_version.ps1`
- Latest test DLL from current source: `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\autofixdim-v113.dll`
- Pre-rewrite `DimensionDrawer.cs` backup requested by user: `D:\work\AI\project\1\DimensionDrawer.cs.bak`
- Historical note: the earlier abandoned `autofixdim-v89.dll` build must not be used as a baseline. The source was rolled back to the `v88` logic before the rejected step-dimension replacement behavior, and the current `v89` suffix is reused for the diameter-style follow-current-child-style test build.

## Commands
- `ASD`: main command.
- `AUTOFIXDIM`: same workflow.
- `AUTOFIXDIMREGEN`: clear old plugin annotations first, then regenerate.
- `AUTOFIXDIMCLEAR`: remove entities carrying XData app name `AUTOFIXDIM`.

## Current Workflow
1. Select outline on layer `DRAWING`.
2. Confirm datum edge, defaulting to outline `MinX` / `MinY`, or specify datum points.
3. Window-select hole geometry; plugin recognizes circles and thread arcs from that selection.
4. If no holes are recognized from the window selection, prompt for manual circle selection.
5. If pin holes exist, prompt the user to pick one pin hole as the datum hole.
6. If a datum hole is picked, prompt X and Y reference points for that datum hole's own position dimensions.
7. After each datum X/Y point, prompt tolerance mode: default `A` means no `±0.05`; entering `S` adds `<>±0.05`.
8. Recognize MainOutline chamfers/fillets from outline geometry only.
9. Collect linear dimensions into Bottom / Top / Left / Right buckets.
10. Flush stacked dimensions with arrow/text collision avoidance and current suppression rules.
11. Place chamfer/fillet callouts with lightweight `DrawJig`: move mouse to preview, click to place, Enter to skip current group, Esc to cancel remaining corner callouts.
12. Place diameter/thread callouts with lightweight `DrawJig`: move mouse to preview, click to place, Enter to skip current group, Esc to cancel remaining diameter callouts.
13. For pin-hole diameter callouts, insert block `CadAider_国标粗糙度16下` if the drawing already contains that block definition.

## Source Map
- `Commands.cs`: command entrypoints and main workflow.
- `GeometryCollector.cs`: outline selection, hole source collection, datum prompts, datum-hole pick prompts.
- `FeatureRecognizer.cs`: outline/hole recognition, MainOutline chamfer/fillet recognition, pin marker matching, thread arc/minor-circle logic, U-slot recognition, row grouping, diagnostics.
- `FeatureModels.cs`: `HoleFeature`, `HoleKind`, `DimensionType`, `OutlineSegment`, `OutlineArc`, `ChamferFeature`, `FilletFeature`.
- `RuleConfig.cs`: centralized tolerances and callout text, including pin-hole H7, pin spacing `±0.02`, group spacing `±0.05`, datum location `<>±0.05`, and default datum tolerance mode.
- `DimensionDrawer.cs`: linear position dimensions, pin-group logic, stacked placement, envelope overall dimensions, current experimental step/protrusion rules, chamfer/fillet suppression, corner-feature Jig placement.
- `NativeDiameterDimensioner.cs`: final interactive diameter/thread dimensions, hole callout Jig preview, pin-hole roughness block insertion.
- `AnnotationMetadata.cs`: XData marking and clear logic for app name `AUTOFIXDIM`.
- `LayerManager.cs`: preferred annotation layer is `JEE-DIM标注`, otherwise current layer.
- `deploy_next_version.ps1`: deploys `bin\Debug\AutoFixtureDim.dll` as the next `autofixdim-vNN.dll` in the toolbox DLL folder.

## Rule Configuration
- Pin-hole diameter tolerance is rule-based. Any hole recognized as `HoleKind.Pin` receives `H7`.
- Same-group pin spacing uses `±0.02`.
- Pin-group-to-pin-group locating dimensions use `±0.05`.
- Datum-hole X/Y location tolerance defaults to no tolerance; the user must enter `S` to add `<>±0.05`.
- Keep future rule tweaks in `RuleConfig.cs` first, then only touch drawing code if the rule needs new behavior.

## Hole Recognition
- Normal hole: selected `Circle` inside outline bounds.
- Pin hole: selected `Circle` matched to a CadAider pin marker block.
- Pin marker matching is intentionally tight: concentric matching first, with fallback search radius `max(radius * 0.0, 1.0)`.
- Thread hole: selected `DRAWING` arc with sweep angle at least 270 degrees and a concentric minor circle.
- Thread minor circle provides the thread callout and is suppressed as a separate normal-hole diameter callout.
- Known thread minor diameter map:
  - `4.917` / `5.0` -> `M6`
  - `6.647` / `6.8` -> `M8`
  - `8.376` / `8.5` -> `M10`
  - `10.106` / `10.2` -> `M12`
  - `13.835` / `14.0` -> `M16`
- Diagnostics should show recognized pin/normal/thread holes and explain suppressed circles.
- U-slot recognition detects selected `DRAWING` layer slots made from two parallel lines plus two half-circle arcs. A recognized slot prioritizes its two arc centers by emitting their center-distance dimension, adds external positioning only for the datum-side slot center, and emits one interactive `2-Rx` radius callout. It does not emit diameter callouts or `8x17腰孔` specification text.
- When no pin holes exist, U-slot positioning uses a continuous dimension chain from datum/reference points instead of repeating every slot from the datum edge.

- Same-radius U-slot radius callouts are grouped at placement time, for example two matching `2-R3.5` slots become one `2x2-R3.5` callout.

## Outline / Envelope Dimensions
- Outer contour selection targets `DRAWING` layer objects and treats entity linetype as `ByLayer`; layer linetype filters center/hidden/dashed construction geometry.
- Closed polyline remains preferred; otherwise `GeometryCollector.SelectMainOutlineComponent` selects the best continuous fallback component using layer semantics, lineweight, closed/continuous geometry, and bounding extent.
- OverallWidth is always `MinX -> MaxX` of the real selected MainOutline envelope.
- OverallHeight is always `MinY -> MaxY` of the real selected MainOutline envelope and stays on the left side.
- Envelope recognition includes straight edges, arcs, fillets, chamfers, polyline bulges, and transition edges. Arc cardinal points are added when they fall on the arc.
- Overall dimensions have highest priority and must not be replaced by local step, chamfer tangent, or fillet tangent dimensions.

## Chamfer / Fillet Behavior
- `OutlineFeature` carries `Chamfers` and `Fillets` in addition to vertices, segments, and arcs.
- Chamfers are recognized on MainOutline 45-degree non-axis segments that connect one horizontal and one vertical main edge.
- If a second 45-degree segment is collinear with the first and close enough to be its extension, the chamfer value is calculated from the merged endpoints' `max(dx, dy)`.
- Chamfer callout text follows the diameter/corner callout dimstyle main-unit linear precision (`Dimdec`) for non-integers; integer chamfer values suppress trailing decimals, e.g. `C5` rather than `C5.00`.
- Long structural inclined edges that are not local 45-degree chamfer geometry remain `Inclined Edge`, not `Chamfer`.
- Fillets are recognized only from MainOutline arcs or polyline bulges with reasonable radius and connected outline endpoints.
- Matching chamfer/fillet texts are grouped into repeated callouts such as `2-C1` and `2-R3`.
- Chamfer arrows attach to the real chamfer edge midpoint.
- Fillet/radius arrows attach to the real arc, not a virtual corner.
- Corner feature placement uses lightweight `DrawJig` preview. Preview shows simplified leader/text feedback; final U-slot radius callouts use `Leader` + `MText` so the placement point is not constrained by `RadialDimension`.

## Suppression Rules
- Feature suppression is intentionally narrow.
- Suppress only dimensions that directly repeat a chamfer/fillet: theoretical sharp-corner dimensions, tangent-point dimensions, small feature-local spans, and fully derivable feature dimensions.
- Do not suppress OverallWidth, OverallHeight, main body length/height, functional positioning dimensions, or unrelated local dimensions.
- A widened single-tangent fillet check prevents the wrong local tangent dimension near an end fillet when the intended outer size should include the radius.
- Top/Bottom mirrored duplicate horizontal normal dimensions are deduped across sides; Bottom is kept by default.
- A local horizontal/vertical edge between two chamfers is suppressed when it is derivable from the overall envelope minus the two chamfer projections.
- The abandoned `v89` rule tried to replace a long overall-minus-chamfer width such as `55` with a chamfer projection `5`; it was rejected and removed from source.

## Step / Protrusion Dimensions
- Current `DrawRightStepHeight` is an experimental rewrite from 2026-05-21. It no longer emits one right-side height candidate directly from a vertical segment.
- It groups horizontal outline segments by Y level, takes each level's leftmost point, emits a left-side continuous vertical chain between adjacent levels, then removes the candidate with the longest extension-line reach to avoid a closed dimension chain.
- This rewrite intentionally places those step-chain dimensions on the left side.
- Bottom horizontal protrusion width no longer blindly selects the longest internal horizontal segment; it targets lower local protrusions and can resolve the span from nearby vertical boundaries.
- A user-requested source backup before the `DrawRightStepHeight` rewrite exists at `D:\work\AI\project\1\DimensionDrawer.cs.bak`.
- The step/protrusion rules are still under AutoCAD visual testing. Prefer adjusting the classifier/chain logic deliberately rather than adding isolated longest-segment heuristics.

## Hole Position Principles

### Pin Holes
- Pin holes are grouped before any normal/thread hole positioning is emitted.
- The first pin-hole group starts from the user-selected datum pin when available; otherwise the left/bottom pin is used as a fallback base.
- The first pin-group base pin is positioned from the outline/datum edges.
- Second and later pin-group base pins are positioned from the previous pin-group base pin, not repeatedly from the outline.
- Pin-group-to-pin-group locating dimensions use `±0.05`.
- Same-group pin spacing is emitted from the group base pin to each other pin in the group and uses `±0.02`.
- Zero-length dimensions are skipped.

### Normal And Thread Holes
- Normal holes are not high-precision pin holes.
- Normal-hole position dimensions do not get `±0.02` or `±0.05`.
- Normal holes do not establish or transfer pin-group datum.
- Normal holes are positioned from the nearest available pin reference, with pin-group bases preferred on ties.
- Thread holes follow the same nearest pin-reference positioning principle as normal holes.
- Thread holes never become pin-group bases and do not participate in pin-group datum transfer.
- If no pin holes exist, normal/thread holes fall back to outline/datum-edge positioning.

## Diameter / Roughness Callouts
- Diameter/thread callout placement uses lightweight `DrawJig` preview to avoid sluggish `DiametricDimension` dynamic-block redraws.
- Final callouts are still real AutoCAD `DiametricDimension` entities.
- Pin-hole diameter callouts insert block `CadAider_国标粗糙度16下` if the block definition exists in the current drawing.
- The roughness block is rotated 180 degrees and marked with `AUTOFIXDIM` XData.
- Current implementation inserts the roughness block at the final dimension text position. If the user requires the exact geometric midpoint of the actual horizontal dimension line, inspect the generated anonymous dimension block and derive the longest horizontal segment midpoint.

## Current Code State
- `DimensionDrawer.DrawHolePositionFromDatumHole` contains the active datum-hole path.
- `BuildPinGroupPlan` builds the pin-group datum chain. The selected datum pin anchors the first group.
- `EmitFirstPinGroupBaseLocation` positions the first group base from the outline/datum edge.
- `EmitPinGroupBaseTransfers` positions later group bases from the previous group base.
- `EmitSameGroupPinDistances` emits same-group `±0.02` pin spacing.
- `EmitNonPinHoleLocations` positions normal/thread holes from the nearest pin reference.
- `DrawSlotDimensions` emits U-slot center-distance dimensions, then `DrawSlotAnchorLocations` emits only the datum-side slot-center external positioning dimension.
- `DrawSlotDimensions` uses continuous slot positioning when there are no pin holes.
- `PickSlotAnchorPoint` selects the datum-side slot center by geometry: horizontal slots choose the center closer to `datum.BaseX`; vertical slots choose the center closer to `datum.BaseY`.
- `DrawSlotRadiusLeadersWithJig` groups same-radius U-slot radius callouts, for example `2x2-R3.5`.
- `RecognizeOutlineCornerFeatures` fills `outline.Chamfers` and `outline.Fillets`.
- `RecognizeChamfers` stores a numeric chamfer `Value`; chamfer display text is re-formatted during drawing from the active diameter/corner callout dimstyle linear precision, with integer values shown without trailing decimals.
- Chamfer extension handling can merge nearby collinear 45-degree segments before calculating the callout value.
- `DrawCornerFeatureLeadersWithJig` handles chamfer/fillet callout placement.
- `DrawRightStepHeight` currently emits a left-side continuous vertical chain from horizontal outline levels and removes the longest extension-line candidate.
- `DrawLowerRightStepWidth` targets lower local protrusion widths and can resolve endpoints from nearby vertical boundaries.
- `NativeDiameterDimensioner.PromptDiameterDimensions` handles diameter/thread callout placement and pin roughness block insertion.

## Known Issues / Follow-Up
- Step-dimension rules are in active redesign. The current `v113` source uses a user-requested left-side continuous chain experiment for vertical step dimensions; it needs AutoCAD verification before treating it as stable.
- The next durable design should still classify step candidates before emitting dimensions: `Overall`, `FeatureDerived`, `FeatureProjection`, `StructuralStep`, and `AmbiguousSmallStep`.
- The user prefers not to keep the abandoned `v89` replacement rule. Do not reintroduce the behavior that replaced long structural widths with chamfer projections.
- Watch for accidental closed dimension chains. The current experimental rule deletes the candidate with the longest extension-line reach, but this should be verified against real fixture outlines.
- Validate whether lower protrusion widths and chamfer-extension values match the user's intended blue-guide dimensions.
- Roughness block placement may still need exact horizontal-line midpoint extraction from the generated dimension block if text-position anchoring is not visually correct.
- `AnnotationPreview.cs` is legacy support; the active hole and corner placement flow uses `DrawJig`.
- Current latest test DLL from source is `bin\Debug\autofixdim-v113.dll`; it was compiled with `DebugSymbols=false` / `DebugType=none` because AutoCAD can lock `bin\Debug\AutoFixtureDim.pdb`.

## Verification
- Standard build command:
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /v:minimal`
- If AutoCAD locks `bin\Debug\AutoFixtureDim.pdb`, use a versioned no-PDB build folder, for example:
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:DebugSymbols=false /p:DebugType=none /p:OutputPath=bin\Debug\vNNNbuild\ /v:minimal`
- Standard deploy command:
  `powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1`
- Latest compiled DLL:
  `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\v113build\AutoFixtureDim.dll`
- Latest test DLL:
  `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\autofixdim-v113.dll`

## Manual AutoCAD Test Checklist
- Load `D:\work\AI\project\autocad-net-c-autocad-autocad-net\bin\Debug\autofixdim-v113.dll` with `NETLOAD` for the current local test build.
- Run `ASD`.
- Select outline and hole geometry.
- Pick the intended first-group datum pin.
- For datum-hole X/Y tolerance prompts, press Enter or enter `A` for no tolerance; enter `S` to add `<>±0.05`.
- Confirm pin-hole diameter callouts always include `H7`, for example `2-%%c8H7`.
- Confirm same-group pin spacing shows `±0.02`.
- Confirm pin-group-to-pin-group locating dimensions show `±0.05`.
- Confirm normal holes do not receive `±0.02` or `±0.05`.
- Confirm thread holes do not receive pin-group tolerance rules.
- Confirm each U-slot emits the two-center distance and exactly one external positioning pair to the datum-side slot center.
- Confirm horizontal U-slots choose the center closer to `datum.BaseX`; vertical U-slots choose the center closer to `datum.BaseY`.
- Confirm U-slot points do not produce diameter callouts or `8x17腰孔` text.
- Confirm same-radius U-slot radius callouts are grouped, for example `2x2-R3.5`.
- Confirm no zero-length dimensions are emitted.
- Confirm OverallWidth/OverallHeight measure the full envelope including chamfers and fillets.
- Confirm `DrawRightStepHeight` emits the left-side vertical chain from horizontal outline levels, removes the longest extension-line candidate, and avoids a closed dimension chain.
- Confirm lower protrusion widths are emitted from the intended local vertical-boundary span, not from the longest internal horizontal segment.
- Confirm 45-degree chamfers use merged collinear extension segments when present and display integer values without trailing decimals while non-integers follow the callout dimstyle linear precision.
- Confirm chamfer/fillet leaders attach to real geometry.
- Test Jig placement for corner callouts and diameter/thread callouts.
- Confirm pin-hole roughness block appears only when block `CadAider_国标粗糙度16下` exists in the drawing.
- Confirm `AUTOFIXDIMCLEAR` removes generated dimensions/callouts/roughness blocks only.
