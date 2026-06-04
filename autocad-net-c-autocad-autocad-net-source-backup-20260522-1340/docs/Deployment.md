# Deployment And Manual Verification

## Test DLL Naming

- Local test DLLs use uppercase `LB` suffixes: `autofixdim-LB<N>.dll`.
- After compiling, copy `bin\Debug\AutoFixtureDim.dll` to the next available `autofixdim-LB<N>.dll`.
- If AutoCAD locks a loaded DLL, increment the suffix and load the fresh DLL.
- Do not use the abandoned historical `autofixdim-v89.dll` behavior as a baseline.

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
2. Run `ASD`, `ASD2`, or `ASD4` depending on the validation target.
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
