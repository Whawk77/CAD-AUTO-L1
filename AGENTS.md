# AUTOFIXDIM / ASD Project Rules

## Safety
- Do not batch-delete files or directories.
- Do not use `del /s`, `rd /s`, `rmdir /s`, `Remove-Item -Recurse`, or `rm -rf`.
- If deletion is required, delete only one explicit file path at a time.
- If bulk deletion seems necessary, stop and ask the user to handle it manually.

## Documentation Rules
- Do not update project documentation or `AGENTS.md` automatically; the user decides when documentation or agent rules should be updated.
- When the user asks for documentation updates after new features or behavior changes, update `PROJECT_HANDOFF.md` first.
- Only write rules into `AGENTS.md` when future AI coding agents would likely make mistakes without reading them.
- Do not put one-off development logs or single-session changelogs into `AGENTS.md`.
- Do not use relative time words such as "today", "yesterday", or "recently"; use absolute dates instead.

## Environment
- Active L1 workspace: `D:\work\AI\project\L1`
- Active source: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340`
- AutoCAD 2020 install path: `D:\Program Files\Autodesk\AutoCAD 2020`
- Build command for local test DLLs: `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /p:PostBuildEvent= /p:DebugType=None /p:DebugSymbols=false /v:minimal`
- Use `/p:PostBuildEvent=` unless the user explicitly wants the project post-build copy to run.
- If AutoCAD locks `bin\Debug\AutoFixtureDim.pdb`, keep `DebugSymbols=false` and `DebugType=None`.
- Build output: `bin\Debug\AutoFixtureDim.dll`
- User-requested test DLL folder: `D:\work\AI\project\L1\autocad-net-c-autocad-autocad-net-source-backup-20260522-1340\bin\Debug`
- Deployment folder: `D:\app\不加班的小刘_工具箱\dll`
- Deploy script: `.\deploy_next_version.ps1`
- Current user test DLL names use uppercase LB suffixes: `autofixdim-LBN.dll`; after compiling, increment the LB number and copy `bin\Debug\AutoFixtureDim.dll` to the new test DLL name.
- Latest test DLL from current source: `bin\Debug\autofixdim-LB21.dll`.
- Historical note: the earlier abandoned `autofixdim-v89.dll` build must not be used as a baseline. The source was rolled back to the `v88` logic before the rejected step-dimension replacement rule, and the current `v89` suffix is reused for the diameter-style follow-current-child-style test build.
- AutoCAD may lock a loaded DLL; if copy fails because the DLL is busy, increment the suffix and load the fresh DLL.

## Product Constraints
- Main command is `ASD`.
- Keep compatibility commands: `AUTOFIXDIM`, `AUTOFIXDIMREGEN`, `AUTOFIXDIMCLEAR`.
- Never create a `CENTER` layer.
- Never generate hole centerlines or cross centerlines.
- Generated annotation entities must be marked with XData app name `AUTOFIXDIM` so `AUTOFIXDIMCLEAR` removes only plugin output.
- Hole recognition is based on the current user selection/window selection, not a full model-space scan.

## Configuration Rules
- Keep machining rules centralized in `RuleConfig.cs` where possible.
- Any hole recognized as `HoleKind.Pin` gets `PinHoleFitToleranceText` (`H7` currently), regardless of diameter.
- Same-group pin spacing uses `PinCenterDistanceToleranceText` (`±0.02` currently).
- Pin-group-to-pin-group distance uses `PinGroupDistanceToleranceText` (`±0.05` currently).
- Datum-hole X/Y location tolerance is controlled by the datum prompt: default is `A` / no tolerance; entering `S` adds `DatumHoleLocationToleranceText` (`<>±0.05` currently).

## Recognition Rules
- Outer contour selection targets objects on layer `DRAWING`.
- Outer contour objects are treated as `Linetype = ByLayer`; use the layer linetype to filter center/hidden/dashed construction geometry.
- MainOutline selection prioritizes layer semantics, lineweight, closed/continuous geometry, and outside bounding extent.
- If no closed polyline exists, fallback chooses the best continuous selected `DRAWING` line/arc/polyline MainOutline component instead of blindly using every selected outline object.
- OverallWidth and OverallHeight must use the full real envelope (`MinX -> MaxX`, `MinY -> MaxY`) of the selected MainOutline, including arcs, fillets, chamfers, and transition edges.
- Overall dimensions have highest priority and must not be replaced by local step, chamfer, or fillet tangent dimensions.
- Chamfers require a non-axis 45-degree MainOutline segment connected to one horizontal and one vertical main edge. If a second 45-degree segment is collinear with the first and close enough to be its extension, the chamfer callout value uses the merged endpoints' `max(dx, dy)`.
- After normal chamfer and fillet recognition, `RecognizeInnerGrooveChamfers(outline)` supplements missing 45/135-degree inner-groove chamfers so their `C...` leaders are emitted by the normal corner-feature path.
- Inner-groove chamfer supplement is for structures that form chamfer plus internal horizontal/vertical groove line plus chamfer or fillet; do not use it as a general long inclined-edge classifier.
- Vertical inner-groove chamfer detection must verify that the internal vertical line's other end connects to another chamfer/45-degree segment or fillet before accepting the groove relationship.
- Chamfer callout text follows the diameter/corner callout dimstyle main-unit linear precision (`Dimdec`) for non-integers; integer values are emitted without trailing decimals, e.g. `C5` not `C5.00`.
- Fillets are recognized only from MainOutline arcs or polyline bulges with reasonable radius and connected outline endpoints.
- Diagnostic output is disabled by default in normal runs. Only re-enable `FeatureRecognizer.DiagnosticsEnabled` for targeted debugging.
- U-slot recognition is incremental: only detect selected `DRAWING` layer slots made from two parallel lines plus two half-circle arcs. Prioritize the two arc centers by emitting their center-distance dimension, add external positioning only for the datum-side slot center, and group same-radius interactive radius callouts such as `2x2-R3.5`; do not emit diameter callouts or slot specification text.

## Feature Suppression Rules
- Chamfer/fillet suppression must stay local to dimensions that directly repeat the feature definition.
- Suppress dimensions to theoretical sharp corners, chamfer tangent/cut points, fillet tangent local spans, and dimensions fully derivable from the feature size.
- Do not suppress OverallWidth, OverallHeight, main body length/height, functional positioning dimensions, or unrelated local dimensions.
- For fillet ends, avoid emitting the wrong tangent-only local size when the intended manufacturing logic is full outer size plus radius.
- Suppress mirrored duplicate horizontal local dimensions across Top/Bottom when they have the same arrow interval and text; keep Bottom by default.
- Suppress mirrored duplicate vertical local dimensions across Left/Right when they have the same Y interval and override text, are `DimensionType.Normal`, and are not `ForceOuterLevel`; keep Left by default.
- Suppress a local horizontal/vertical edge between two chamfers when it is derivable from the overall envelope minus the two chamfer projections.
- Do not reintroduce the abandoned `v89` behavior that replaced an overall-minus-chamfer long width such as `55` with a chamfer projection `5`.
- Current side step-dimension rules are experimental: `DrawRightStepHeight` builds a left-side chain from the leftmost points of horizontal outline levels, while `DrawRightSideStepHeight` builds the right-side chain from the rightmost points.
- Top and bottom horizontal step-width rules collect structure points from local horizontal outline levels and use side-specific ignored-point rules before emitting `_topDims` / `_bottomDims`.
- For top/bottom point sets, only upper-half inner-groove chamfer endpoints may enter Top and only lower-half inner-groove chamfer endpoints may enter Bottom.
- For left/right point sets, inner-groove chamfer endpoints are handled with the same intent as top/bottom: ignore the endpoint connected to the groove's internal vertical line, then let chain dimensions span the remaining structural width/height.
- Directional 45/135-degree endpoint suppression is side-specific. Top/bottom use horizontal groove context; left/right use vertical groove context. Keep this logic in `AddDirectionalInclinedEndpoint`, `AddSideDirectionalInclinedEndpoint`, and their `IsInnerGroove...` helpers rather than scattering one-off ignores.
- Bottom horizontal step width currently targets local protrusion widths near the lower outline, resolving the span from nearby vertical boundaries where possible instead of blindly selecting the longest internal horizontal segment.

## Leader and Attachment Rules
- All dimension and leader attachment points must land on real geometry: entity edges, arcs, chamfer edges, fillet arcs, real endpoints, or real intersections.
- Do not attach to theoretical intersections, extension-line intersections, virtual sharp corners, centerlines, auxiliary lines, dimension lines, projected points, or floating points.
- Chamfer leader arrows attach to the real chamfer edge; fillet/radius arrows attach to the real arc.
- Corner feature and hole diameter callout placement use lightweight `DrawJig` previews: move mouse to preview, click to place, Enter to skip the current group, Esc to cancel the remaining callouts.
- Final chamfer leaders are `Leader` + `MText`; final fillet callouts use `RadialDimension`; final hole/thread callouts use `DiametricDimension`.
- Corner-feature leader lines follow the current layer and current entity color (`CECOLOR`); corner-feature `MText` stays on the annotation layer and uses the diameter/corner callout dimstyle text color.
- Pin-hole diameter callouts insert block `CadAider_国标粗糙度16下` when the block definition exists in the drawing. The block is rotated 180 degrees, marked with `AUTOFIXDIM` XData, and currently follows the dimension text position rather than a measured anonymous-dimension horizontal-line midpoint.

## Hole Position Rules
- Hole positioning follows pin-first datum transfer: first pin group, later pin groups, same-group pin spacing, then normal/thread holes.
- The first pin-group base pin is positioned from the outline/datum edges.
- Later pin-group base pins are positioned from the previous pin-group base pin, not repeatedly from the outline.
- Same-group pin spacing is emitted from the group base pin to the other pins and uses `±0.02`.
- Pin-group-to-pin-group transfer dimensions use `±0.05`.
- Hole-position dimensions should choose their per-side bucket by geometry: lower/upper holes go Bottom/Top, left/right holes go Left/Right.
- If a pin-group base is centered between opposite sides within `GeometryTolerance`, choose the side opposite the datum edge to avoid datum-side overlap.
- Normal holes are not high-precision pin holes and never participate in pin-group datum transfer.
- Normal holes are positioned from the nearest available pin reference, preferring pin group bases on ties.
- Thread holes follow the same positioning rule as normal holes and never become pin-group bases.
- If no pin holes exist, normal/thread holes fall back to outline/datum-edge positioning.
- Zero-length dimensions must never be emitted.

## Dimension Stacking Rules
- All linear dimensions are deferred into per-side buckets: Bottom / Top / Left / Right.
- `FlushStackedDimensions(outline)` places them in one pass.
- Pre-dedup removes dimensions with identical arrow interval plus identical override text.
- Sort by span ascending: short dimensions inside, long dimensions outside.
- Placement uses strict arrow conflict, text-box conflict, and outline coverage checks.
- Chain dimensions sharing endpoints may share a level; nested or overlapping arrows stack outward.
- Hole-position dimensions keep native extension lines intact. Do not suppress native extension lines or draw segmented replacement lines by default.

## Style Rules
- Prefer annotation layer `JEE-DIM标注`; otherwise use the current layer.
- Linear dimensions should prefer current drawing dimstyles named `SCALE-1-01`, `SCALE-1-02`, `SCALE-1-03`, `SCALE-1-04`, `SCALE-1-06`, `SCALE-1-08`, or `SCALE-1-10`.
- Diameter/thread callout dimensions should prefer AutoCAD child style `SCALE-1-01$3`.
- Use AutoCAD diameter control text `%%c`, not Unicode diameter symbols, for diameter callouts.
