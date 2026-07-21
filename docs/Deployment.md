# Deployment And Manual Verification

## Test DLL Naming

- Direct regression builds use a new output folder ending in `-vNNN` for every build; never reuse an earlier folder.
- Versioned folder names should end in `-vNNN`. When continuing an existing local version sequence, inspect `bin\` for the highest used number and increment; historical guidance started at `-v190` and continued `-v191`, `-v192`, …
- Core tests and full plugin builds must not share the same `-vNNN` output directory (see `docs/DimensionLayoutRegression.md`).
- The legacy manual helper `run-cad-test.ps1` copies `bin\Debug\AutoFixtureDim.dll` to an uppercase `LB` filename such as `autofixdim-LB<N>.dll`.
- The `LB` copy flow is not the four-direction automated regression flow.
- If AutoCAD locks a loaded DLL, use a fresh `-vNNN` build output for regression instead of overwriting it.
- Do not use the abandoned historical `autofixdim-v89.dll` behavior as a baseline.
- A current loadable set for day-to-day work may live under `bin\Debug\` (gitignored); keep only the newest intended set there.

## CAD Test Script

Helper script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-cad-test.ps1
```

Restart flow:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-cad-test.ps1 -Restart
```

Important behavior:

- The script builds the project, copies a versioned DLL when `$DllPath` is empty, generates `scripts\cad-test.scr`, starts AutoCAD, loads the DLL, and runs the configured command.
- This is a manual smoke-test helper. Use `docs/DimensionLayoutRegression.md` for the repeatable four-direction regression.
- Do not run this script unless the user explicitly asks for compilation or CAD validation.
- `-Restart` asks existing AutoCAD windows to close; it does not force-kill AutoCAD.

## Deployment Script

Deployment script:

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1
```

Use it only when the user explicitly asks for deployment.

## Manual AutoCAD Flow

Typical manual verification:

1. Load the current versioned DLL with `NETLOAD`.
2. Run `ASD` for normal generation or `ASD4` for diagnostic generation.
3. Select the outline and hole/source geometry.
4. Pick datum information when prompted.
5. Inspect generated dimensions, leaders, callouts, XData cleanup, and diagnostic labels.

## Diagnostic Flow

`ASD4` clears generated plugin annotations, then regenerates with diagnostic labels and a side prompt:

- `All`
- `Top`
- `Bottom`
- `Left`
- `Right`

Diagnostic mode is for isolating layout issues. It should not be confused with `FeatureRecognizer.DiagnosticsEnabled`, which controls recognizer command-line logs and is disabled by default.

## Four-Direction Automated Regression

The reusable Top, Bottom, Left, and Right coordinate-driven workflow is documented in `docs/DimensionLayoutRegression.md`.

It covers:

- fresh `-vNNN` build outputs;
- fixed fixture preparation and SHA256 verification;
- AutoLISP-driven selection and datum input;
- per-side diagnostic report archiving and validation;
- AutoCAD PNG export and visual review.

Do not run this workflow unless the user explicitly authorizes build and AutoCAD testing.
