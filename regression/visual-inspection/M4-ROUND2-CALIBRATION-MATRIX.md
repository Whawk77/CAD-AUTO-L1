# M4 Round 2 calibration matrix

Visual inspection remains warning-only. This matrix records calibration evidence; it does not change CAD PASS/FAIL.

## Round 1 archived false-positive modes

| Side | Per-repeat count | Mode | Disposition for round 2 |
| --- | ---: | --- | --- |
| Bottom | 1 | `TEXT_ARROW_COLLISION` from different-dimension bounds with only a 0.125 single-axis shallow overlap | Require hard two-axis overlap above tolerance |
| Bottom | 2 | `EXTENSION_TEXT_COLLISION` at legitimate dimension-line ends | Exclude the end allowance |
| Top | 1 | `EXTENSION_TEXT_COLLISION` at a legitimate dimension-line end | Exclude the end allowance |
| Left | 1 | `TEXT_ARROW_COLLISION` from different-dimension bounds with only a 0.125 single-axis shallow overlap | Require hard two-axis overlap above tolerance |
| Left | 1 | `EXTENSION_TEXT_COLLISION` at a legitimate dimension-line end | Exclude the end allowance |

The five grouped rows above represent six warning patterns (Bottom 3, Top 1, Left 2 per repeat); this task did not execute CAD.

## Confirmed synthetic defects

| Group | Required warning code(s) | Purpose |
| --- | --- | --- |
| B | `DIM_SOURCE_CROSSING`, `TEXT_SOURCE_COLLISION` | Source geometry crossing |
| C1 | `TEXT_ARROW_COLLISION` | Different-dimension deep box collision |
| C2 | `EXTENSION_TEXT_COLLISION` | Extension-line middle collision |

## Legal-contact counterexamples

| Group | Expected warning count | Contact |
| --- | ---: | --- |
| D1 | 0 | Same-dimension text and arrow bounds overlap |
| D2 | 0 | Different dimensions have exactly 0.125 X overlap and deep Y overlap, below the scale-derived hard-collision threshold |
| D3 | 0 | An extension reaches a foreign text box only at its dimension-line endpoint |

## Round 2 results

| Evidence | Result |
| --- | --- |
| Synthetic Windows PowerShell 5.1 self-check (2026-07-30) | Passed: A clean, B/C defects, D legal contacts |
| CAD replay / visual report | Batch `20260730-175938404` with `Debug-v3`: 5×3 = 15/15 Passed; visual 15/15 Completed; warning/FP/FN = 0; 5 groups each have `uniqueNormalizedHashes=1`; all DL01 image baselines were `Matched` across three repeats; OC01 was `NoPinnedImages`. |

## Real saved final DWG positive evidence

The following evidence is from the user-saved final DWG, not a synthetic self-check. It remains `WarningOnly` with `blocking=false`.

| Evidence | Result |
| --- | --- |
| Authoritative DWG | `regression/visual-inspection/M4-positive-collisions.dwg`; SHA-256 `79D944E913627459872C6D94F120198E0FBD92105E77A0D409CEF8C9A3ECDD88` |
| Debug-v3 DLLs | `AutoFixtureDim.dll` `B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`; `CadAuto.Core.dll` `4D20C161DF40BFB0C5B67C6D498438D42F9EBC7EA5B2FCA1BB1CFD0BA34E9498`; `CadAuto.CadAdapter.dll` `D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857` |
| Positive runs | `20260731-140227787` and `20260731-140239044`; both normalized visual reports are `CC166E6294769A232020C893BCD6276B5C95C22189C9B4C9D96F0E9E0BA57B57` |
| Top region | `TEXT_TEXT_COLLISION` persisted in both runs (handles `ED7303`, `ED7304`, `ED7331`, `ED733A`) |
| Left `20 / 10±0.05` region | User-confirmed, low-priority layout defect; both runs remain `detected=false`, `manualReviewRequired=true` (persistent miss) |
| Clean batch | `20260731-140918570`: 5 cases × 2 rounds, 10/10 CAD passed, 0 visual warnings, deterministic normalized visual reports across the two rounds, Core 130/130 passed |

This positive evidence records current detector coverage only; it does not update product code, expectations, fixtures, or manifests.

## Final calibrated evidence

`LEFT_LAYOUT_TEXT_PLACEMENT` is a dedicated `WarningOnly` rule (`blocking=false`), not a `TEXT_TEXT_COLLISION`: it considers only left-side vertical DIMs and warns when its own text-box center Y lies beyond the two extension-point Y values by more than `max(DIMTXT, DIMASZ) = 3.5`.

| Evidence | Result |
| --- | --- |
| Positive DWG | SHA-256 `79D944E913627459872C6D94F120198E0FBD92105E77A0D409CEF8C9A3ECDD88` |
| Positive runs | `20260731-143336491`, `20260731-143348058`; both `Completed`, `WarningOnly`, `blocking=false` |
| New layout warning | Both runs: `LEFT_LAYOUT_TEXT_PLACEMENT` on `ED7311`, `ED7347` |
| Existing collision warning | Both runs: `TEXT_TEXT_COLLISION` on `ED7303`, `ED7304`, `ED7331`, `ED733A` |
| Positive determinism | Both normalized visual reports: `45F2AB97E2C978D647C72D6D030FF8F798A2D1C4FA96D01CE2CE9A072D373C18` |
| Image and trace determinism | Both runs: `full.png` `130BF718F3A68EB063C7F52E0C4E8814035CA42983E79E6536B6F4E64901CB2C`; `detail-1.png` `CF39AF9421C7F665F2B40FC67B602E3F689C0D7A9BDC0EC3E3459438051EF0D2`; `detail-2.png` `3337695A6B742401662A7F554E933B8A7C780D189E2A5B0148EA54D6062E3BA1`; `trace.log` `1E5F1557112C492619C3A3EA0FF8CD416223550128692A7BA3DB54582562D0D9` |
| Evidence locations | Each positive run directory contains `result.json`, `visual-report.json`, `trace.log`, `full.png`, `detail-1.png`, `detail-2.png`, and `hashes.json` under `regression/visual-inspection/runs/M4-positive-collisions-real/<run>/`; raw report paths vary, normalized reports are stable |
| Clean regression | `regression/nightly/runs/20260731-143417741/summary.json`: OC01 plus DL01 Bottom/Top/Left/Right across 2 rounds, 10/10 Passed; every visual `warningCount=0`; `falsePositiveTotal=0`; `falseNegativeTotal=0`; each case `uniqueNormalizedHashes=1`; Core 130/130 |
| DLL identity | `AutoFixtureDim.dll` `B145FDCCC2C5DFA05FCBEA5E8886BC3AD27B4A577D0366EFFD2762A53990CBED`; `CadAuto.Core.dll` `4D20C161DF40BFB0C5B67C6D498438D42F9EBC7EA5B2FCA1BB1CFD0BA34E9498`; `CadAuto.CadAdapter.dll` `D3961973C6F6152789B969BCFF9EC607803B6602D29D774B4CB959DC03CC6857` |

This calibration did not modify product code, expectations, fixtures, or manifests. PowerShell 7 runner compatibility and upgrade-blocking gates are deferred to separate work.

## Independent M4 Round 2 verification / WarningOnly evidence passed

`LEFT_LAYOUT_TEXT_PLACEMENT` remains dedicated to left-side vertical DIMs; Top and Right DIMs do not enter the rule. It remains `WarningOnly` with `blocking=false`.

| Evidence | Result |
| --- | --- |
| Independent positive runs | `20260731-145519058`, `20260731-145525171`; fixture SHA-256 `79D944E913627459872C6D94F120198E0FBD92105E77A0D409CEF8C9A3ECDD88`; both normalized visual reports `45F2AB97E2C978D647C72D6D030FF8F798A2D1C4FA96D01CE2CE9A072D373C18` |
| Persistent detections | Both runs: `LEFT_LAYOUT_TEXT_PLACEMENT` (`ED7311`, `ED7347`) and `TEXT_TEXT_COLLISION` (`ED7303`, `ED7304`, `ED7331`, `ED733A`) |
| Independent visual determinism | Both runs: `full.png` `130BF718F3A68EB063C7F52E0C4E8814035CA42983E79E6536B6F4E64901CB2C`; `detail-1.png` `CF39AF9421C7F665F2B40FC67B602E3F689C0D7A9BDC0EC3E3459438051EF0D2`; `detail-2.png` `3337695A6B742401662A7F554E933B8A7C780D189E2A5B0148EA54D6062E3BA1`; `trace.log` `1E5F1557112C492619C3A3EA0FF8CD416223550128692A7BA3DB54582562D0D9` |
| pwsh clean batch | `regression/nightly/runs/20260731-145316634/summary.json`: pwsh outer entry; 5 clean cases × 2 = 10/10 Passed; 10 visual Completed; 0 warnings; FP/FN 0/0; 5/5 deterministic; Core 130/130; same Debug-v3 DLL hashes recorded above |
| Runner compatibility | Only `scripts/run-cad-regression-suite.ps1` changed: resolution order `powershell.exe` → fixed Windows PowerShell path → `pwsh` → current process; both test entry points share it. No CAD rule, expectation, fixture, or manifest changed. |
| Environment note | The first pwsh run lacked Windows PowerShell modules and `Get-FileHash` failed. Final verification used pwsh outer execution with the Windows PowerShell Modules path explicitly inherited; this is environment-compatibility evidence, not a product defect. |

WarningOnly 证据通过；阻断升级未申请。
