# Architecture Refactor Plan

## Goal

Keep the current AutoCAD annotation behavior stable while making feature rules easier to change, test, and diagnose.

## Target Boundaries

```text
AutoCAD command handlers
  -> application workflow
  -> CadAuto.Core (feature recognition and dimension planning)
  -> CadAuto.CadAdapter (read CAD entities and render the plan)
```

- `CadAuto.Core` must not reference AutoCAD assemblies or AutoCAD-specific types.
- `CadAuto.CadAdapter` converts AutoCAD entities into core inputs and converts a dimension plan into drawing entities.
- The entry project owns command registration, document access, and user-facing messages only.

## Incremental Delivery Plan

### Phase 1: Repository Hygiene

1. Keep build output, diagnostics, and local AutoCAD artifacts ignored by Git.
2. Remove already-tracked generated files in a dedicated, reviewed change. Do not mix that cleanup with behavior changes.
3. Keep DWG fixtures and expected regression results under version control.

### Phase 2: Command Entry Points

1. Retain the existing AutoCAD command names.
2. Move shared execution from `Commands` into an application workflow service.
3. Move the `AG1` special workflow into its own service.
4. Move diagnostic report creation into a dedicated diagnostic service.

### Phase 3: Stable Core Contract

Use explicit data flow between the layers:

```text
CadSelection -> PartGeometry -> RecognizedFeatures -> DimensionPlan -> CadRenderResult
```

The command layer should orchestrate this flow without implementing feature-recognition or layout rules.

### Phase 4: Regression Safety

1. Test core recognition and dimension-planning rules without AutoCAD.
2. Store representative `DimensionPlan` snapshots for holes, slots, chamfers, fillets, and four-side layouts.
3. Keep a small AutoCAD regression suite for entity conversion and rendering.

## First Code Change

Extract the normal `ASD` execution path first, without changing output. This is the highest-value low-risk step because the existing command surface stays intact while subsequent feature work becomes more localized.

## Non-Goals

- Do not rewrite the AutoCAD plugin framework.
- Do not change the current annotation rules during structural extraction.
- Do not delete generated files in bulk; handle tracked artifact cleanup as a separate reviewed action.
