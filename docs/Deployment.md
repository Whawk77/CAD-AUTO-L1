# Deployment And Manual Verification

## Test DLL Naming

- Every loadable/test DLL build uses a fresh `bin\Debug-vN\` folder; never reuse an earlier folder or deliver a bare overwrite of `bin\Debug\`.
- Inspect `bin\Debug-v*` before every build and use the highest numeric `N` plus one. This workspace starts at `Debug-v1` when no version exists.
- Keep the complete build set together: `AutoFixtureDim.dll`, `CadAuto.Core.dll`, and `CadAuto.CadAdapter.dll`.
- Core tests and full plugin builds must not share the same `-vNNN` output directory (see `docs/DimensionLayoutRegression.md`).
- The legacy manual helper `run-cad-test.ps1` copies `bin\Debug\AutoFixtureDim.dll` to an uppercase `LB` filename such as `autofixdim-LB<N>.dll`.
- The `LB` copy flow is not the four-direction automated regression flow.
- If AutoCAD locks a loaded DLL, use a fresh `-vNNN` build output for regression instead of overwriting it.
- Do not use the abandoned historical `autofixdim-v89.dll` behavior as a baseline.
- `bin\Debug\` is the default unversioned output, not the versioned deliverable.

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

Deployment script (replace `N` with the already-built version selected for deployment):

```powershell
powershell -ExecutionPolicy Bypass -File .\deploy_next_version.ps1 -SourceDll .\bin\Debug-vN\AutoFixtureDim.dll
```

Use it only when the user explicitly asks for deployment.

- The script copies the complete three-DLL set into a new `<DeployDir>\<BaseName>-vN\` directory and returns its `AutoFixtureDim.dll` path for `NETLOAD`; it does not compile the plugin.
- The deployment sequence considers both legacy `<BaseName>-vN.dll` files and version directories, independently of the `bin\Debug-vN` build sequence. Previous deployments are preserved.
- Every copied DLL must match its source SHA256 before the script reports success.

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
