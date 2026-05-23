# AUTOFIXDIM / ASD Project Rules

## Safety
- Do not batch-delete files or directories.
- Do not use `del /s`, `rd /s`, `rmdir /s`, `Remove-Item -Recurse`, or `rm -rf`.
- If deletion is required, delete only one explicit file path at a time.
- If bulk deletion seems necessary, stop and ask the user to handle it manually.

## Environment
- Workspace: `D:\work\AI\project\autocad-net-c-autocad-autocad-net`
- AutoCAD 2020 install path: `D:\Program Files\Autodesk\AutoCAD 2020`
- Build command: `dotnet msbuild AutoFixtureDim.csproj /p:Configuration=Debug /v:minimal`
- If AutoCAD locks `bin\Debug\AutoFixtureDim.pdb`, compile to a versioned no-PDB folder with `DebugSymbols=false`, `DebugType=none`, and a custom `OutputPath`.
- Build output: `bin\Debug\AutoFixtureDim.dll`
- Deployment folder: `D:\app\不加班的小刘_工具箱\dll`
- Deploy script: `.\deploy_next_version.ps1`
- Deployed DLL names use lowercase base name: `autofixdim-vNN.dll`.
- Latest test DLL from current source: `bin\Debug\autofixdim-v113.dll`.
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
- Chamfer callout text follows the diameter/corner callout dimstyle main-unit linear precision (`Dimdec`) for non-integers; integer values are emitted without trailing decimals, e.g. `C5` not `C5.00`.
- Fillets are recognized only from MainOutline arcs or polyline bulges with reasonable radius and connected outline endpoints.
- Diagnostic output should explain recognized pin/normal/thread holes, suppressed circles, and recognized chamfers/fillets.
- U-slot recognition is incremental: only detect selected `DRAWING` layer slots made from two parallel lines plus two half-circle arcs. Prioritize the two arc centers by emitting their center-distance dimension, add external positioning only for the datum-side slot center, and group same-radius interactive radius callouts such as `2x2-R3.5`; do not emit diameter callouts or slot specification text.

## Feature Suppression Rules
- Chamfer/fillet suppression must stay local to dimensions that directly repeat the feature definition.
- Suppress dimensions to theoretical sharp corners, chamfer tangent/cut points, fillet tangent local spans, and dimensions fully derivable from the feature size.
- Do not suppress OverallWidth, OverallHeight, main body length/height, functional positioning dimensions, or unrelated local dimensions.
- For fillet ends, avoid emitting the wrong tangent-only local size when the intended manufacturing logic is full outer size plus radius.
- Suppress mirrored duplicate horizontal local dimensions across Top/Bottom when they have the same arrow interval and text; keep Bottom by default.
- Suppress a local horizontal/vertical edge between two chamfers when it is derivable from the overall envelope minus the two chamfer projections.
- Do not reintroduce the abandoned `v89` behavior that replaced an overall-minus-chamfer long width such as `55` with a chamfer projection `5`.
- Current vertical step-dimension test rule is intentionally experimental: `DrawRightStepHeight` builds a left-side continuous chain from the leftmost points of horizontal outline levels, then removes the candidate with the longest extension-line reach to avoid a closed dimension chain.
- Bottom horizontal step width currently targets local protrusion widths near the lower outline, resolving the span from nearby vertical boundaries where possible instead of blindly selecting the longest internal horizontal segment.

## Leader and Attachment Rules
- All dimension and leader attachment points must land on real geometry: entity edges, arcs, chamfer edges, fillet arcs, real endpoints, or real intersections.
- Do not attach to theoretical intersections, extension-line intersections, virtual sharp corners, centerlines, auxiliary lines, dimension lines, projected points, or floating points.
- Chamfer leader arrows attach to the real chamfer edge; fillet/radius arrows attach to the real arc.
- Corner feature and hole diameter callout placement use lightweight `DrawJig` previews: move mouse to preview, click to place, Enter to skip the current group, Esc to cancel the remaining callouts.
- Final chamfer leaders are `Leader` + `MText`; final fillet callouts use `RadialDimension`; final hole/thread callouts use `DiametricDimension`.
- Pin-hole diameter callouts insert block `CadAider_国标粗糙度16下` when the block definition exists in the drawing. The block is rotated 180 degrees, marked with `AUTOFIXDIM` XData, and currently follows the dimension text position rather than a measured anonymous-dimension horizontal-line midpoint.

## Hole Position Rules
- Hole positioning follows pin-first datum transfer: first pin group, later pin groups, same-group pin spacing, then normal/thread holes.
- The first pin-group base pin is positioned from the outline/datum edges.
- Later pin-group base pins are positioned from the previous pin-group base pin, not repeatedly from the outline.
- Same-group pin spacing is emitted from the group base pin to the other pins and uses `±0.02`.
- Pin-group-to-pin-group transfer dimensions use `±0.05`.
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
