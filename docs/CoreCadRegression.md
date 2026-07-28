# Core Test ↔ Minimal DWG Regression

## Purpose

`regression/core-cad` links selected Core planner tests to minimal real DWG fixtures. It is a targeted production-pipeline gate, not a replacement for the full Core suite.

A registered case runs:

1. mapped `CadAuto.Core.Tests` filters;
2. an optional fresh `bin\Debug-vN` plugin build;
3. a writable fixture copy in an ignored run directory;
4. AutoCAD 2020 Core Console with generated LSP/SCR;
5. deterministic `ASDREPRO` selection;
6. diagnostic report capture;
7. structured final/suppressed candidate assertions.

All GUI AutoCAD instances must be closed while extracting a fixture. Regression itself uses `accoreconsole.exe` and only requires that no other Core Console regression is active. Scripts never force-kill AutoCAD on timeout.

## Initial case

`OC01-bottom-outer-step-partition` covers the real 90 = 70 + 20 outline:

- keep `OverallWidth 90`;
- keep the real `BottomStructWidth 20` MinY step;
- suppress both `BottomStructWidth 70` and `OutlineSegment 70` body remainders.

The fixture is extracted from the 21 LINE handles recorded in the schema-v2 diagnostic report. The immutable fixture hash is stored in `regression/core-cad/cases.json`.

## Extract fixture once

With AutoCAD closed and the source diagnostic/DWG still hash-identical:

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\extract-core-cad-fixture.ps1 `
  -CaseId OC01-bottom-outer-step-partition
```

If the source DWG was saved after the diagnostic run but the recorded source handles remain authoritative, add `-AllowSourceHashChange`. This only relaxes the source-file hash check; every recorded handle must still exist and be a LINE.

Extraction leaves `fixtureReady=false`. A case becomes ready only after a genuine passing regression.

## Bring-up run

Build a fresh plugin and run the mapped Core tests plus AutoCAD regression:

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\run-core-cad-regression.ps1 `
  -CaseId OC01-bottom-outer-step-partition `
  -Build `
  -AllowNotReady
```

Current product behavior may intentionally fail a newly introduced expectation. Failure artifacts remain under:

```text
regression\core-cad\runs\<case-id>\<timestamp>\
```

A passing bring-up automatically changes `fixtureReady` to `true`.

## Normal run

```powershell
powershell -ExecutionPolicy Bypass `
  -File .\scripts\run-core-cad-regression.ps1 `
  -CaseId OC01-bottom-outer-step-partition `
  -Build
```

Without `-Build`, the runner uses the highest existing `bin\Debug-vN` directory, or accepts `-DllDirectory` explicitly.

## Case manifest contract

Each case defines:

- mapped Core test filters;
- immutable fixture path/hash and expected source entity count/type;
- command scope and diagnostic side;
- required final dimensions;
- forbidden final dimensions;
- required candidate decisions.

Expectations match by semantic role, approximate value, side, orientation, and optional decision reason. Do not encode candidate IDs; IDs can change as planning evolves.
