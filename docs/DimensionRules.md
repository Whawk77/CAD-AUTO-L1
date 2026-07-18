# Dimension Rules

## Overall Dimensions

- Overall width and height use the full real envelope of the selected `MainOutline`.
- Overall dimensions have highest priority.
- Do not replace overall dimensions with local step, chamfer tangent, fillet tangent, or feature projection dimensions.

## Attachment Rules

- All dimension and leader attachment points must land on real geometry.
- Allowed attachment targets include entity edges, arcs, chamfer edges, fillet arcs, real endpoints, and real intersections.
- Do not attach to theoretical intersections, extension-line intersections, virtual sharp corners, centerlines, auxiliary lines, dimension lines, projected points, or floating points.
- Chamfer leader arrows attach to real chamfer edges.
- Fillet/radius arrows attach to real arcs.

## Pin And Hole Position Dimensions

- Hole positioning follows pin-first logic when pin holes exist.
- The first pin group base pin is positioned from the outline/datum edge.
- Current source behavior positions later pin-group base pins from the first pin-group base pin.
- Same-group pin spacing is emitted from the group base pin to other pins and uses `PinCenterDistanceToleranceText`.
- Pin-group transfer dimensions use `PinGroupDistanceToleranceText`.
- Normal holes are not high-precision pin holes and do not establish or transfer pin-group datum.
- Thread holes follow normal-hole positioning and never become pin-group bases.
- If no pin holes exist, normal/thread holes fall back to outline/datum-edge positioning.
- Zero-length dimensions must never be emitted.

## Functional Holes And Loose Holes

- Normal/thread holes assigned to a pin group are functional-hole dimensions owned by that pin group.
- Pin-group functional-hole dimensions may prefer local boundary placement.
- Loose/scatter hole chains use `LooseChainId` and may prefer a nearby valid local boundary when it shortens extension lines.
- A loose/scatter `HoleLocation` dimension line must remain outside the real contour interior and must not be sent to a distant global side merely to reduce crowding.
- Loose-hole chain dimensions should stay visually grouped and should not be split across unrelated sides by post-processing.

## Local Boundary Rules

- Local boundary placement is allowed for:
  - `PinDistance`
  - `PinGroupDistance`
  - functional-hole dimensions marked with `PreferLocalBoundary`
  - loose/scatter `HoleLocation` dimensions marked with `PreferLocalBoundary`
- A local-boundary dimension line must not enter the real outer contour interior.
- If no valid local boundary is found, fall back to the global `MainOutline` envelope.

## Step And Structure Dimensions

- Four side-specific step/structure flows exist:
  - Top: `DrawTopStepWidth`
  - Bottom: `DrawLowerRightStepWidth`
  - Left: `DrawRightStepHeight`
  - Right: `DrawRightSideStepHeight`
- Top and bottom horizontal width rules collect side-specific structure points and ignored points before emitting dimensions.
- Left and right vertical height rules collect side-specific structure points and ignored points before emitting dimensions.
- Directional 45/135-degree endpoint suppression is side-specific.
- Top/bottom use horizontal groove context.
- Left/right use vertical groove context.
- Do not scatter one-off ignored-point rules when existing directional helpers can express the behavior.

## Top Diagnostic Rules

`ASD4 -> Top` currently emits point diagnostics for top structure-width debugging.

Known labels:

- `SP:VTM`: vertical top-most structure point.
- `SP:TIE`: top inclined endpoint.
- `SP:TSE`: top slope endpoint.
- `IG:XOUT`: ignored because extension crosses outline.
- `IG:D45`: ignored directional 45-degree endpoint.
- `IG:IGR`: ignored inner-groove shared endpoint.

Current follow-up areas:

- `SP:TSE` may still fail to match some intended slope endpoints.
- `SP:VTM` may be too broad and include right-side height structure points that should not drive top width.
- Bottom/Left/Right diagnostics currently have less detailed ignored-point reasons than Top.

## Suppression And Stacking

- Chamfer/fillet suppression must stay local to dimensions that directly repeat the feature definition.
- Suppress dimensions to theoretical sharp corners, chamfer tangent/cut points, fillet tangent local spans, and dimensions fully derivable from feature size.
- Do not suppress overall width, overall height, main body size, functional positioning dimensions, or unrelated local dimensions.
- Suppress mirrored duplicate horizontal local dimensions across Top/Bottom only when they measure the same geometry and are not functionally significant.
- Suppress mirrored duplicate vertical local dimensions across Left/Right only when they measure the same geometry and are not functionally significant.
- Same-side duplicate measured dimensions should keep the rule-significant candidate, preferring tolerance text, smaller tolerance, functional dimension type, forced outer-level dimensions, then non-empty override text.
- Linear dimensions are deferred into Bottom/Top/Left/Right buckets and placed by `FlushStackedDimensions`.
- Same-side dimensions first follow the reading hierarchy: local hole spacing, intra-group pin spacing, datum/group-transfer spacing, then overall size.
- Within one reading level, shorter spans are generally placed inside; longer spans stack outward, with original generation order as the stable tie-breaker.
- A rooted datum chain may align its datum, inter-group transfer, and intra-group pin distances on one shared dimension line when consecutive members share endpoints and have no strict arrow overlap; a text or arrow conflict moves the whole chain outward.
- Functional-hole dimensions belonging to one pin group share a separate same-orientation alignment line and do not join the rooted datum chain.
- Placement checks arrow conflicts, text-box conflicts, and outline coverage.
- Shared-extension alignment may move eligible dimensions outward, but must not move dimensions whose real arrow endpoints touch another dimension at the same dimension-line level.

## Style And Callouts

- Prefer annotation layer `JEE-DIM标注`; otherwise use the current layer.
- Linear dimensions prefer current drawing dimstyles named `SCALE-1-01`, `SCALE-1-02`, `SCALE-1-03`, `SCALE-1-04`, `SCALE-1-06`, `SCALE-1-08`, or `SCALE-1-10`.
- Diameter/thread callouts prefer matching `$3` child styles, then `SCALE-1-01$3`, then fallback style.
- Use AutoCAD diameter control text `%%c`.
- Final chamfer leaders are `Leader` + `MText`.
- Final fillet callouts use `RadialDimension`.
- Final hole/thread callouts use `DiametricDimension`.
- Corner-feature leader lines follow current layer and current entity color.
- Corner-feature text stays on the annotation layer and uses diameter/corner callout dimstyle text color.
