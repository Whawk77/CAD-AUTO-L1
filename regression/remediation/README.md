# Remediation case truth matrix

`case-truth-matrix.json` records product truth separately from current test output.

## Status meanings

- `ConfirmedCorrect`: repository evidence identifies the expectation as an approved product invariant, user-confirmed production behavior, or a genuinely passed fixed fixture.
- `KnownWrong`: the product behavior is explicitly confirmed wrong. A failing test or report alone is not enough.
- `Unresolved`: evidence cannot yet decide product truth. Do not infer correct or wrong from the current implementation.

The current matrix has no `KnownWrong` case. Core P0/P1, OC01, and all four DL01 directions have explicit evidence; Core P2/P3 and HS01-HS08 remain unresolved. DL01 was promoted as the second `ConfirmedCorrect` batch after target validation passed 4/4 and the full/detail images from runs `20260730-130500001` through `20260730-130500004` were reviewed.

## Promotion rules

- Promote `Unresolved` to `ConfirmedCorrect` only with explicit user approval or a traceable genuine run against a pinned input. DL01 additionally requires approved full/detail image review.
- Promote `Unresolved` to `KnownWrong` only after the product owner confirms the behavior is defective.
- Move `KnownWrong` to `ConfirmedCorrect` only after an authorized behavior change and fresh traceable evidence.
- Keep the input hash and evidence index with every promotion.

Never change an expectation, baseline, fixture hash, or `fixtureReady` merely to make validation green. Baseline changes require separate approval and evidence from the newly approved product contract.

## Validation

From the repository root:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\validate-remediation-matrix.ps1
```

The validator rejects malformed JSON, an unsupported schema version, duplicate or unexpected IDs, missing fixed cases, illegal states, and `ConfirmedCorrect` entries without meaningful evidence or a matching repository-local input SHA256.
