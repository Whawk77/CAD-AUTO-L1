# AUTOFIXDIM / ASD Handoff

Last synced: 2026-05-28

## 2026-05-28 Project Handoff Summary

### 1. Current Project Goal
- Maintain and iterate the AutoCAD 2020 .NET Framework plugin for semi-automatic fixture-part annotation.
- Active command remains `ASD`; compatibility command `AUTOFIXDIM` remains. Regenerate command is `ASD2`; clear command is `ASD3`.
- Current development focus is loose/scatter non-pin hole position layout: grouping, chain dimensions, duplicate suppression, side selection, and interaction with pin-hole datum dimensions.

### 2. Completed Work
- The current source was rolled back to the source corresponding to `autofixdim-LB34.dll`, then loose-hole layout work continued from that baseline.
- `DimensionDrawer.cs` now has internal loose-hole planning models: `LooseHoleLineGroup`, `LooseHoleMacroGroup`, and `LooseHoleLocationPlan`.
- Loose normal/thread holes are grouped by same specification plus same X or same Y axis, with long gaps split into separate natural chains.
- Loose line groups and singleton loose holes are clustered into macro groups by spatial proximity.
- Macro groups choose a pin reference and anchor loose hole by mutual-nearest logic, with fallback to shortest pin-base-to-hole distance.
- Loose-hole center distances are emitted before loose-hole location dimensions.
- Loose-hole location prefers chain-style positioning from the selected pin reference or already located nearby loose hole.
- Loose-chain dimensions carry `LooseChainId`; post-processing avoids moving a single dimension out of its chain.
- Same-side and cross-side duplicate suppression were tightened so repeated measured dimensions prefer tolerance/pin/datum/overall significance over plain duplicates.
- Per the 2026-05-28 requirement, loose/scatter `HoleLocation` dimensions no longer use local boundary placement.
- Local boundary placement is retained only for `PinDistance` and `PinGroupDistance`; loose-chain dimensions are excluded from local boundary lookup.
- Latest compiled test DLL from current source: `bin\Debug\autofixdim-LB51.dll`.

### 3. Key Modified Files
- `DimensionDrawer.cs`: loose-hole grouping, macro grouping, chain IDs, loose-chain alignment, duplicate suppression, pin-distance local boundary behavior, and removal of loose-hole local boundary placement.
- `PROJECT_HANDOFF.md`: this handoff summary and current test DLL references.
- `中文注释.md`: untracked local note file exists; do not treat it as an official project document unless the user explicitly asks.

### 4. Current Known Problems
- `autofixdim-LB51.dll` still needs AutoCAD visual verification on the user's fixture drawings.
- Loose-hole chains now align on a shared side, but after removing local boundary usage they use the global outline side baseline; visual distance may still need side/layer policy tuning.
- Pin-hole center distance and pin-group locating dimensions can use local boundaries; their behavior near recesses/protrusions still needs manual confirmation.
- Cross-side duplicate suppression should be checked on drawings that intentionally keep mirrored dimensions.
- Loose macro grouping uses a heuristic threshold, so unusual drawings may still over-group or under-group loose holes.
- Short loose-position dimensions use heuristic reference selection/text fitting; some cases may still need manual visual tuning.
- There are no automated AutoCAD visual tests; validation is by `NETLOAD` plus manual `ASD` runs.

### 5. Next Tasks
- Load `bin\Debug\autofixdim-LB51.dll` in AutoCAD and run `ASD` on the highlighted loose-hole cases from 2026-05-28.
- Confirm same-row loose-hole chains are visually collinear and not split across unintended levels or sides.
- Confirm loose-hole dimensions no longer bind to local outline boundaries.
- Confirm pin-hole `50±0.02` and pin-group `105±0.05` style dimensions still keep the intended tolerance and side behavior.
- Verify duplicate suppression does not remove required functional dimensions.
- If LB51 visual output is accepted, keep LB51 as the current handoff baseline; otherwise continue from current source and compile LB52 or later.

### 6. Key Rules And Constraints
- Do not create centerlines or a `CENTER` layer.
- Generated objects must carry XData app name `AUTOFIXDIM` so `ASD3` removes only plugin-generated annotations.
- Pin holes keep `H7`; same-group pin spacing uses `±0.02`; pin-group locating dimensions use `±0.05`.
- Normal/thread loose-hole location dimensions must not inherit pin tolerance text.
- `RuleConfig.cs` remains the preferred place for machining-rule constants and tolerance text.
- Loose/scatter `HoleLocation` dimensions must not use local boundary placement unless the user explicitly reverses the 2026-05-28 decision.
- `PinDistance` and `PinGroupDistance` may use local boundary placement, but should not place dimension lines inside the real outer contour.
- Do not reintroduce the abandoned `autofixdim-v89.dll` behavior that replaced long structural widths with chamfer projections.
- Do not batch-delete files or directories. Use only one explicit file path per deletion, and never use recursive delete commands.

### 7. Important Commands
- Build:
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`
- Current test DLL:
  `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB51.dll`
- AutoCAD load command: `NETLOAD`, then select the current test DLL.
- Main command: `ASD`
- Regenerate command: `ASD2`
- Clear generated annotations: `ASD3`
- Standard deploy script when explicitly needed:
  `powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1`

### 8. Notes
- AutoCAD can lock loaded DLL/PDB files. If copying over an existing test DLL fails, build without debug symbols and create a new `autofixdim-LB<N>.dll` suffix.
- The current working tree contains modified `DimensionDrawer.cs`; do not assume the checked-out source is identical to a committed baseline.
- The latest user-requested loose-hole rule change is: remove local boundary usage for loose/scatter hole dimensions, while keeping chain alignment behavior.
- `AGENTS.md` is a rule file, not a changelog; avoid writing session history there.
- Use exact dates in documentation instead of relative words.

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

## Recent Source Changes
- `DimensionDrawer.cs`: pin-group planning now keeps the user-selected datum pin as the first group's `BasePin`. Later groups are recognized first, then their `BasePin` is chosen from within the completed group using the previous group's `BasePin` as the locating reference.
- `DimensionDrawer.cs`: pin-group base dimensions choose Bottom/Top and Left/Right placement from the pin group base's nearest outline side. Same-group pin distances currently choose side from the midpoint between the two pins. Normal/thread holes assigned to a pin group currently use that group's selected side; only the no-pin fallback uses the target hole's own nearest side.
- `DimensionDrawer.cs`: when a hole is centered between opposite sides within `GeometryTolerance`, pin-group placement falls back to the side opposite the datum edge, reducing overlap with datum-side dimensions.
- `DimensionDrawer.cs`: corner-feature leader preview/final leader lines now follow the current AutoCAD layer and `CECOLOR`; corner-feature `MText` uses the diameter/corner callout dimstyle text color.
- `DimensionDrawer.cs` and `FeatureRecognizer.cs`: inner-groove chamfer recognition now requires the other end of the internal vertical groove line to connect to another 45-degree chamfer, known chamfer, or fillet. This prevents a single loose vertical line from promoting a long inclined structural edge into an inner-groove chamfer.
- `DimensionDrawer.cs`: same-side duplicate measured dimensions are now deduped by measured arrow interval and span instead of requiring identical override text. When duplicates measure the same geometry, the retained dimension prefers explicit tolerance text first, then the smaller tolerance value, then the functional dimension type priority, then forced outer-level dimensions, then non-empty override text.
- `DimensionDrawer.cs`: post-layout shared-extension alignment can move a candidate dimension outward to the nearest suitable outer dimension. The block rule checks actual 2D arrow endpoint contact; sharing only the same X or Y coordinate at a different dimension-line level does not block alignment.
- `Commands.cs`: chamfer/fillet, U-slot radius, and hole diameter/thread interactive Jig placement now runs after the main linear dimensions are committed. Diameter/thread callouts are ordered by spatial hole group; inside each group the order is pin holes, thread holes, then normal holes.
- `NativeDiameterDimensioner.cs`: hole callout Jig prints the full callout instruction once per group, then uses a short `Pick point:` AcquirePoint prompt during movement.
- `FeatureRecognizer.cs`: previous hole/chamfer/fillet diagnostic logs are gated by `DiagnosticsEnabled = false` and should stay quiet in normal AutoCAD runs.

## Project
- Active L1 workspace: `D:\work\AI\project\L1`
- Active source: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`
- Project file: `AutoFixtureDim.csproj`
- Target framework: .NET Framework 4.7.2
- AutoCAD install: `D:\Program Files\Autodesk\AutoCAD 2020`
- Build command: `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`
- Build output: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\AutoFixtureDim.dll`
- Deployment folder: `D:\app\不加班的小刘_工具箱\dll`
- Deploy script: `.\deploy_next_version.ps1`
- Latest test DLL from current source: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB51.dll`
- Pre-rewrite `DimensionDrawer.cs` backup requested by user: `D:\work\AI\project\1\DimensionDrawer.cs.bak`
- Historical note: the earlier abandoned `autofixdim-v89.dll` build must not be used as a baseline. The source was rolled back to the `v88` logic before the rejected step-dimension replacement behavior, and the current `v89` suffix is reused for the diameter-style follow-current-child-style test build.

## Commands
- `ASD`: main command.
- `AUTOFIXDIM`: same workflow.
- `ASD2`: clear old plugin annotations first, then regenerate.
- `ASD3`: remove entities carrying XData app name `AUTOFIXDIM` from non-XRef, non-dependent block table records.

## Current Workflow
1. Select outline on layer `DRAWING`.
2. Confirm datum edge, defaulting to outline `MinX` / `MinY`, or specify datum points.
3. Window-select hole geometry; plugin recognizes circles, thread arcs, slot arcs, and slot lines from that selection.
4. If no holes are recognized from the window selection, prompt for manual circle selection.
5. If pin holes exist, prompt the user to pick one pin hole as the datum hole.
6. If a datum hole is picked, prompt X and Y reference points for that datum hole's own position dimensions.
7. After each datum X/Y point, prompt tolerance mode: default `A` means no `±0.05`; entering `S` adds `<>±0.05`.
8. Recognize MainOutline chamfers/fillets from outline geometry only.
9. Collect linear dimensions into Bottom / Top / Left / Right buckets.
10. Flush stacked dimensions with arrow/text collision avoidance, current suppression rules, and post-layout shared-extension alignment.
11. Commit the main automatic linear dimensions before any interactive callout Jig starts.
12. Place chamfer/fillet callouts with lightweight `DrawJig`: move mouse to preview, click to place, Enter to skip current group, Esc to cancel remaining corner callouts.
13. Place U-slot radius callouts with lightweight `DrawJig`: same-radius slots are grouped, for example `2x2-R3.5`.
14. Place diameter/thread callouts with lightweight `DrawJig`: spatial hole groups are processed in order; inside each group annotate pin holes, then thread holes, then normal holes.
15. For pin-hole diameter callouts, insert roughness block `CadAider_国标粗糙度16下` if the drawing already contains that block definition.

## Source Map
- `Commands.cs`: command entrypoints, main workflow, post-linear interactive callout orchestration, and diameter Jig placement ordering.
- `GeometryCollector.cs`: outline selection, hole source collection, datum prompts, datum-hole pick prompts.
- `FeatureRecognizer.cs`: outline/hole recognition, MainOutline chamfer/fillet recognition, pin marker matching, thread arc/minor-circle logic, U-slot recognition, row grouping, diagnostics gated by `DiagnosticsEnabled`.
- `FeatureModels.cs`: `HoleFeature`, `HoleKind`, `DimensionType`, `OutlineSegment`, `OutlineArc`, `ChamferFeature`, `FilletFeature`.
- `RuleConfig.cs`: centralized tolerances and callout text, including pin-hole H7, pin spacing `±0.02`, group spacing `±0.05`, datum location `<>±0.05`, and default datum tolerance mode.
- `DimensionDrawer.cs`: linear position dimensions, pin-group logic, stacked placement, shared-extension alignment, envelope overall dimensions, current experimental step/protrusion rules, chamfer/fillet suppression, corner-feature Jig placement.
- `NativeDiameterDimensioner.cs`: final interactive diameter/thread dimensions, hole callout Jig preview, short AcquirePoint prompt, pin-hole roughness block insertion.
- `AnnotationMetadata.cs`: XData marking and clear logic for app name `AUTOFIXDIM`; generated entities carry `GroupId` in XData.
- `LayerManager.cs`: preferred annotation layer is `JEE-DIM标注`, otherwise current layer.
- `DimStyleManager.cs`: linear dimensions keep the current dimstyle if it is one of the preferred `SCALE-1-0x` styles; diameter callouts first try the selected linear style's `$3` child, then fall back to `SCALE-1-01$3`, then the linear style.
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
- A user-picked datum pin can be matched by `ObjectId`; if that fails, the source accepts a center/radius tolerance match of `0.01`. If the picked circle is reasonable (`0.5 <= radius <= 100.0`) and inside the outline, it is accepted as a manual datum pin even if it was not in the automatically recognized pin list.
- Pin marker matching is intentionally tight: concentric matching first, with fallback search radius `max(radius * 0.0, 1.0)`.
- Thread hole: selected `DRAWING` arc with sweep angle at least 270 degrees and a concentric minor circle.
- Thread minor circle provides the thread callout and is suppressed as a separate normal-hole diameter callout.
- Known thread minor diameter map:
  - `4.917` / `5.0` -> `M6`
  - `6.647` / `6.8` -> `M8`
  - `8.376` / `8.5` -> `M10`
  - `10.106` / `10.2` -> `M12`
  - `13.835` / `14.0` -> `M16`
- Diagnostic output is disabled by default through `DiagnosticsEnabled = false`; re-enable it only for targeted debugging.
- U-slot half arcs are accepted within about 15 degrees of a semicircle. Two-arc slots require matching radii within `max(GeometryTolerance, radius * 0.02)`, same-X or same-Y alignment within about `radius * 0.05`, and exactly two connecting `DRAWING` lines.
- Single-arc U-slots are also recognized from one half arc plus two parallel lines touching opposite arc endpoints. Their center distance is `0`, so no center-distance dimension is emitted; radius leaders target the real arc midpoint.
- Slot source entities are suppressed from normal hole/circle processing after a slot is recognized. Slot center points are represented as `HoleKind.Slot` only for positioning flow and are excluded from diameter callout grouping.
- U-slot recognition detects selected `DRAWING` layer slots made from two parallel lines plus two half-circle arcs. A recognized slot prioritizes its two arc centers by emitting their center-distance dimension, adds external positioning only for the datum-side slot center, and emits one interactive `2-Rx` radius callout. It does not emit diameter callouts or `8x17腰孔` specification text.
- When no pin holes exist, U-slot positioning uses a continuous dimension chain from datum/reference points instead of repeating every slot from the datum edge.
- When pin groups exist, U-slot anchor positioning uses the datum-side slot center and locates it from the nearest pin reference; group base pins win tie-breaks through the pin-group reference ordering.

- Same-radius U-slot radius callouts are grouped at placement time, for example two matching `2-R3.5` slots become one `2x2-R3.5` callout.
- Diameter callout groups exclude slot points and are separated by spatial hole group, then `HoleKind`, diameter, fit tolerance, and thread callout text. Inside each spatial group, the interactive order is pin holes, thread holes, then normal holes; after that the next spatial group is processed.
- Spatial grouping prefers pin-cluster structure when pins exist, so the holes that belong to the same fixture-hole group are placed together. With no pin clusters, the fallback uses the current row/spatial order.

## Outline / Envelope Dimensions
- Outer contour selection targets `DRAWING` layer objects and treats entity linetype as `ByLayer`; layer linetype filters center/hidden/dashed construction geometry.
- Closed polyline remains preferred; otherwise `GeometryCollector.SelectMainOutlineComponent` selects the best continuous fallback component using layer semantics, lineweight, closed/continuous geometry, and bounding extent.
- Outline selection intentionally avoids a hard DXF selection filter so old `POLYLINE` entities and mixed selected geometry still reach project diagnostics.
- Fallback outline entities may be lines, arcs, lightweight polylines, or old `Polyline2d` entities. Confirmed thread arcs are excluded from fallback outline candidates.
- Fallback component scoring favors layer semantics first (`DRAWING`, then layer names containing `OUTLINE`/`CONTOUR`), then layer/entity lineweight, closedness, continuity count, length, and bounding area.
- OverallWidth is always `MinX -> MaxX` of the real selected MainOutline envelope.
- OverallHeight is always `MinY -> MaxY` of the real selected MainOutline envelope and stays on the left side.
- Envelope recognition includes straight edges, arcs, fillets, chamfers, polyline bulges, and transition edges. Arc cardinal points are added when they fall on the arc.
- Overall dimensions have highest priority and must not be replaced by local step, chamfer tangent, or fillet tangent dimensions.

## Chamfer / Fillet Behavior
- `OutlineFeature` carries `Chamfers` and `Fillets` in addition to vertices, segments, and arcs.
- Chamfers are recognized on MainOutline 45-degree non-axis segments that connect one horizontal and one vertical main edge.
- If a second 45-degree segment is collinear with the first and close enough to be its extension, the chamfer value is calculated from the merged endpoints' `max(dx, dy)`.
- After normal chamfer and fillet recognition, `RecognizeInnerGrooveChamfers(outline)` supplements missing inner-groove chamfers. This is intentionally after `RecognizeFillets(outline)` so chamfer + internal line + fillet structures can be recognized.
- Inner-groove chamfer supplement only accepts 45/135-degree local segments that participate in an internal horizontal or vertical groove relationship. It should not turn long structural inclined edges into chamfer callouts.
- For vertical inner-groove relationships, the internal vertical line must connect at its other end to another chamfer/45-degree segment or fillet. This keeps the supplement limited to real chamfer-line-chamfer or chamfer-line-fillet groove structures.
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
- Left/Right mirrored duplicate vertical normal dimensions are deduped across sides when the Y interval and override text match; Left is kept by default. Overall height, forced outer-level dimensions, and non-normal feature dimensions are not suppressed by this rule.
- Same-side duplicate dimensions are considered duplicates when their arrow interval and measured span match within `GeometryTolerance`, even if their override text differs. The retained duplicate is selected by `CompareDuplicatePreference`.
- Same-side duplicate preference order is: keep a dimension with tolerance text over one without tolerance text; when both have tolerance text, keep the smaller extracted tolerance value such as `±0.02` over `±0.05`; then prefer dimension types in this order: `PinDistance`, `PinGroupDistance`, datum-hole location, overall width/height, normal; then prefer `ForceOuterLevel`; then prefer any non-empty override text.
- Tolerance extraction intentionally recognizes both the correct `±` character and the common mojibake `卤` character so the duplicate keeper still works against legacy encoded override strings.
- After the first stacked layout pass, a linear dimension can align outward to the nearest suitable outer dimension when one of its extension lines overlaps that outer dimension's extension line. This is intended for cases like `50±0.02` aligning to `124±0.05` through a shared extension line.
- A candidate is not aligned when one of its real dimension-line arrow endpoints has 2D point contact with another dimension arrow endpoint. Same X with different Y, or same Y with different X, is not contact and must not block alignment.
- A local horizontal/vertical edge between two chamfers is suppressed when it is derivable from the overall envelope minus the two chamfer projections.
- The abandoned `v89` rule tried to replace a long overall-minus-chamfer width such as `55` with a chamfer projection `5`; it was rejected and removed from source.

## Step / Protrusion Dimensions
- Current `DrawRightStepHeight` is an experimental rewrite from 2026-05-21. It no longer emits one right-side height candidate directly from a vertical segment.
- It groups horizontal outline segments by Y level, takes each level's leftmost point, emits a left-side continuous vertical chain between adjacent levels, then removes the candidate with the longest extension-line reach to avoid a closed dimension chain.
- This rewrite intentionally places those step-chain dimensions on the left side.
- `DrawRightSideStepHeight` separately builds the right-side vertical chain from the rightmost points of horizontal outline levels. Left-side ignored points do not flow into right-side ignored points.
- Top and bottom horizontal step-width rules now use side-specific structure point sets. Top only allows upper-half inner-groove chamfer endpoints into the Top point set; Bottom only allows lower-half inner-groove chamfer endpoints into the Bottom point set.
- Left and right vertical step chains use matching inner-groove behavior: for a side inner groove, the ignored endpoint is the chamfer endpoint connected to the groove's internal vertical line.
- Directional 45/135-degree endpoint suppression should stay in `AddDirectionalInclinedEndpoint`, `AddSideDirectionalInclinedEndpoint`, and the `IsInnerGroove...` helper family. Avoid adding one-off ignored points in the drawing routines.
- Bottom horizontal protrusion width no longer blindly selects the longest internal horizontal segment; it targets lower local protrusions and can resolve the span from nearby vertical boundaries.
- A user-requested source backup before the `DrawRightStepHeight` rewrite exists at `D:\work\AI\project\1\DimensionDrawer.cs.bak`.
- The step/protrusion rules are still under AutoCAD visual testing. Prefer adjusting the classifier/chain logic deliberately rather than adding isolated longest-segment heuristics.

## Hole Position Principles

### Pin Holes
- Pin holes are grouped before any normal/thread hole positioning is emitted.
- The first pin-hole group starts from the user-selected datum pin when available, and that selected pin must remain the first group's `BasePin`; otherwise the left/bottom pin is used as a fallback base.
- Pin groups currently contain the seed pin plus the nearest same-diameter paired pin, when such a pin exists. Later group discovery seeds from the remaining pin nearest to the previous group base; after the pair is formed, the group's `BasePin` is chosen from inside that group against the previous group base.
- The first pin-group base pin is positioned from the outline/datum edges.
- Current source positions second and later pin-group base pins from the first pin-group base pin, not from the immediately previous group and not repeatedly from the outline.
- Pin-group-to-pin-group locating dimensions use `±0.05`.
- Same-group pin spacing is emitted from the group base pin to each other pin in the group and uses `±0.02`.
- Pin-group hole-position dimensions choose their output side from the group base pin: horizontal dimensions go Bottom/Top by nearest outline side, vertical dimensions go Left/Right by nearest outline side.
- Once a group's `BasePin` is fixed, same-group pin distances are emitted from that base pin to the other pins, but their side is chosen from the midpoint of the pin pair rather than from the group's stored side.
- Same-group pin distances must never be affected by another group's side rule; inter-group dimensions use the target group's selected `BasePin` side.
- If a pin-group base pin is geometrically centered between opposite sides, its hole-position dimensions use the side opposite the datum edge.
- Zero-length dimensions are skipped.

### Normal And Thread Holes
- Normal holes are not high-precision pin holes.
- Normal-hole position dimensions do not get `±0.02` or `±0.05`.
- Normal holes do not establish or transfer pin-group datum.
- Normal holes are assigned to the nearest pin pair/group by summed distance to the group's first two pins; ties are resolved by distance to the group `BasePin`.
- Thread holes follow the same pin-group assignment principle as normal holes.
- Thread holes never become pin-group bases and do not participate in pin-group datum transfer.
- If no pin holes exist, normal/thread holes fall back to outline/datum-edge positioning.
- With pin groups present, normal/thread holes are positioned from the assigned group `BasePin` and placed on that group's selected side. With no pin groups, normal/thread holes are positioned from outline/datum edges and placed on the target hole's nearest outline side.

## Diameter / Roughness Callouts
- Diameter/thread callout placement uses lightweight `DrawJig` preview to avoid sluggish `DiametricDimension` dynamic-block redraws.
- Diameter/thread callout placement starts only after the main linear dimensions have been generated and committed.
- Final callouts are still real AutoCAD `DiametricDimension` entities.
- Diameter/thread callout Jig prints the full instruction once per group, then uses a short `Pick point:` AcquirePoint prompt while sampling points. The preview text point defaults to `center + radius * 3` in X and Y; Enter skips the current diameter group and Esc/cancel stops remaining diameter groups.
- Diameter/thread placement order is spatial group by spatial group. Within one spatial group, pin-hole diameter callouts are prompted first, then thread callouts, then normal-hole diameter callouts.
- Corner-feature leader lines use the current AutoCAD layer and current entity color (`CECOLOR`) for both preview and final leaders.
- Corner-feature callout text remains on the annotation layer but takes its color from the active diameter/corner callout dimstyle text color.
- Pin-hole diameter callouts insert block `CadAider_国标粗糙度16下` if the block definition exists in the current drawing.
- The roughness block is rotated 180 degrees and marked with `AUTOFIXDIM` XData.
- Current implementation inserts the roughness block at the final dimension text position. If the user requires the exact geometric midpoint of the actual horizontal dimension line, inspect the generated anonymous dimension block and derive the longest horizontal segment midpoint.

## Current Code State
- `DimensionDrawer.DrawHolePositionFromDatumHole` contains the active datum-hole path.
- `BuildPinGroupPlan` builds the pin-group datum chain. The selected datum pin anchors the first group as its `BasePin`; each group is the seed plus nearest same-diameter paired pin when available, and later groups choose a group-internal `BasePin` against the previous group base after the pair is formed.
- `AssignPinGroupPlacementSides` chooses each pin group's horizontal and vertical output side before hole-position dimensions are emitted.
- `EmitFirstPinGroupBaseLocation` positions the first group base from the outline/datum edge on the selected side.
- `EmitPinGroupBaseTransfers` currently positions every later group base from the first group base on the target group's selected side.
- `EmitSameGroupPinDistances` emits same-group pin spacing with `±0.02` and chooses the output side from the midpoint of the two pins being dimensioned.
- `AssignNonPinHolesToNearestPinPair` assigns normal/thread holes to the nearest pin pair by summed pin distance, then `EmitNonPinHoleLocations` positions them from that group's `BasePin` on the group's selected side.
- `DrawSlotDimensions` emits U-slot center-distance dimensions, then `DrawSlotAnchorLocations` emits only the datum-side slot-center external positioning dimension.
- `DrawSlotDimensions` uses continuous slot positioning when there are no pin holes.
- `AddSingleArcSlotHorizontalDatumDimension` handles horizontal single-arc slots without pin groups by measuring the true center-to-datum span in override text while attaching the extension points to a real outline point and the nearest upper/lower arc grip point.
- `PickSlotAnchorPoint` selects the datum-side slot center by geometry: horizontal slots choose the center closer to `datum.BaseX`; vertical slots choose the center closer to `datum.BaseY`.
- `DrawSlotRadiusLeadersWithJig` groups same-radius U-slot radius callouts, for example `2x2-R3.5`.
- `SuppressDuplicateMeasuredDimensions` runs inside each side's `FlushSide` after sorting by span. It removes same-side duplicate measured dimensions by geometry, and uses `CompareDuplicatePreference`, `GetDimensionPreferenceRank`, and `TryExtractTolerance` to keep the most rule-significant candidate.
- `AlignDimensionsBySharedExtensionLines` runs after stacked placement and can align candidates to the nearest suitable outer dimension by shared extension line.
- `HasArrowEndpointTouch` blocks that alignment only when actual 2D arrow endpoints touch on the dimension line, not when endpoints merely share one coordinate.
- `Commands.DrawPostLinearInteractiveAnnotations` runs chamfer/fillet, U-slot radius, and diameter/thread Jig placement after the automatic linear-dimension transaction is committed.
- `Commands.BuildDiameterGroupsForPlacement` orders diameter/thread Jig groups spatially, then pin/thread/normal inside each spatial group.
- `RecognizeOutlineCornerFeatures` fills `outline.Chamfers` and `outline.Fillets`.
- `RecognizeChamfers` stores a numeric chamfer `Value`; chamfer display text is re-formatted during drawing from the active diameter/corner callout dimstyle linear precision, with integer values shown without trailing decimals.
- Chamfer extension handling can merge nearby collinear 45-degree segments before calculating the callout value.
- `DrawCornerFeatureLeadersWithJig` handles chamfer/fillet callout placement.
- `DrawRightStepHeight` currently emits a left-side continuous vertical chain from horizontal outline levels and removes the longest extension-line candidate.
- `DrawLowerRightStepWidth` targets lower local protrusion widths and can resolve endpoints from nearby vertical boundaries.
- `NativeDiameterDimensioner.PromptDiameterDimensions` handles diameter/thread callout placement, one-time full callout messages, short AcquirePoint prompts, and pin roughness block insertion.
- `FeatureRecognizer.DiagnosticsEnabled` gates hole, slot, chamfer, and fillet diagnostics and is false in the current source.

## Known Issues / Follow-Up
- Step-dimension rules are in active redesign. The current LB51 source still contains experimental four-side point-set and inner-groove endpoint rules; it needs AutoCAD verification before treating it as stable.
- The next durable design should still classify step candidates before emitting dimensions: `Overall`, `FeatureDerived`, `FeatureProjection`, `StructuralStep`, and `AmbiguousSmallStep`.
- The handoff is now aligned to the current source behavior where `EmitPinGroupBaseTransfers` references the first pin group base for all later groups. If the intended design is a true previous-group chain, change the code deliberately and update this document at the same time.
- The handoff is also aligned to the current source behavior where same-group pin spacing uses midpoint side selection and normal/thread holes assigned to pin groups use the owning group's side. If future visual testing expects per-target nearest side, adjust `EmitSameGroupPinDistances` / `EmitNonPinHoleLocations` deliberately.
- The user prefers not to keep the abandoned `v89` replacement rule. Do not reintroduce the behavior that replaced long structural widths with chamfer projections.
- Watch for accidental closed dimension chains. The current experimental rule deletes the candidate with the longest extension-line reach, but this should be verified against real fixture outlines.
- Validate whether lower protrusion widths and chamfer-extension values match the user's intended blue-guide dimensions.
- Roughness block placement may still need exact horizontal-line midpoint extraction from the generated dimension block if text-position anchoring is not visually correct.
- `AnnotationPreview.cs` is legacy support; the active hole and corner placement flow uses `DrawJig`.
- Current latest test DLL from source is `bin\Debug\autofixdim-LB51.dll`; it was compiled with `DebugSymbols=false` / `DebugType=None` because AutoCAD can lock `bin\Debug\AutoFixtureDim.pdb`.
- Post-layout shared-extension alignment still needs real-drawing visual verification. The key acceptance case is that a small dimension sharing an extension line with an outer dimension aligns outward when its arrows do not actually touch another dimension's arrows in 2D.
- Diameter/thread spatial grouping assumes pin clusters define the natural hole groups. Verify no-pin drawings still place callouts in a sensible row/spatial order.

## Verification
- Standard build command:
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`
- If AutoCAD locks `bin\Debug\AutoFixtureDim.pdb`, use a versioned no-PDB build folder, for example:
  `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:DebugSymbols=false /p:DebugType=none /p:OutputPath=bin\Debug\vNNNbuild\ /v:minimal`
- Standard deploy command:
  `powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1`
- Latest compiled DLL:
  `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\AutoFixtureDim.dll`
- Latest test DLL:
  `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB51.dll`

## Manual AutoCAD Test Checklist
- Load `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug\autofixdim-LB51.dll` with `NETLOAD` for the current local test build.
- Run `ASD`.
- Select outline and hole geometry.
- Pick the intended first-group datum pin.
- For datum-hole X/Y tolerance prompts, press Enter or enter `A` for no tolerance; enter `S` to add `<>±0.05`.
- Confirm pin-hole diameter callouts always include `H7`, for example `2-%%c8H7`.
- Confirm same-group pin spacing shows `±0.02`.
- Confirm pin-group-to-pin-group locating dimensions show `±0.05`.
- Confirm later pin-group base transfers currently measure from the first pin-group base, matching source behavior.
- Confirm normal holes do not receive `±0.02` or `±0.05`.
- Confirm thread holes do not receive pin-group tolerance rules.
- Confirm no-pin normal/thread fallback dimensions move to the nearest suitable side: lower holes to Bottom, upper holes to Top, left holes to Left, right holes to Right.
- Confirm normal/thread holes assigned to pin groups use the owning pin group's selected side in the current source.
- Confirm centered pin groups use the side opposite the datum edge instead of colliding with datum-side dimensions.
- Confirm each U-slot emits the two-center distance and exactly one external positioning pair to the datum-side slot center.
- Confirm single-arc U-slots emit no center-distance dimension, still emit radius leader callouts, and attach datum dimensions to real outline/arc grip points when no pin group exists.
- Confirm horizontal U-slots choose the center closer to `datum.BaseX`; vertical U-slots choose the center closer to `datum.BaseY`.
- Confirm U-slot points do not produce diameter callouts or `8x17腰孔` text.
- Confirm same-radius U-slot radius callouts are grouped, for example `2x2-R3.5`.
- Confirm no zero-length dimensions are emitted.
- Confirm same-side duplicate measured dimensions keep the rule-significant override instead of keeping the first/shortest textual duplicate: `±0.02` should beat `±0.05`, a tolerance override should beat blank text, and pin/datum/overall types should not be lost to plain normal dimensions measuring the same span.
- Confirm OverallWidth/OverallHeight measure the full envelope including chamfers and fillets.
- Confirm `DrawRightStepHeight` emits the left-side vertical chain from horizontal outline levels, removes the longest extension-line candidate, and avoids a closed dimension chain.
- Confirm lower protrusion widths are emitted from the intended local vertical-boundary span, not from the longest internal horizontal segment.
- Confirm 45-degree chamfers use merged collinear extension segments when present and display integer values without trailing decimals while non-integers follow the callout dimstyle linear precision.
- Confirm inner-groove chamfer recognition does not classify a long structural inclined edge as a chamfer unless the internal vertical groove line connects to another chamfer/fillet at its other end.
- Confirm chamfer/fillet leaders attach to real geometry.
- Confirm corner-feature leader lines follow the current layer/current `CECOLOR`, while the callout text follows the diameter/corner dimstyle text color.
- Confirm normal runs do not print `[diagnostic]` or `[诊断]` hole/chamfer/fillet logs.
- Confirm chamfer/fillet, U-slot radius, and diameter/thread Jig prompts start only after the automatic linear dimensions have been generated and committed.
- Confirm diameter/thread Jig order is spatial group by spatial group, with pin holes before thread holes before normal holes inside each group.
- Confirm post-layout shared-extension alignment moves eligible dimensions outward, but does not move a dimension whose real arrow endpoint has 2D point contact with another arrow endpoint on the same dimension-line level.
- Confirm the hole callout command line prints the full instruction once per group and uses the short `Pick point:` prompt during Jig movement.
- Test Jig placement for corner callouts and diameter/thread callouts.
- Confirm pin-hole roughness block appears only when block `CadAider_国标粗糙度16下` exists in the drawing.
- Confirm `ASD3` removes generated dimensions/callouts/roughness blocks only.
