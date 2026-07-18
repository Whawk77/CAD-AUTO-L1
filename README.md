# AUTOFIXDIM / ASD

AutoCAD 2020 .NET Framework plugin for semi-automatic fixture-part annotation.

Main command: `ASD`

Compatibility and helper commands:

- `AUTOFIXDIM`: same main workflow.
- `ASD4`: clear generated annotations, then run diagnostic generation with side selection.
- `ASD5`: generate outline-related annotations only.
- `ASD6`: generate hole-related annotations only.
- `ASD7`: generate corner-feature annotations only.
- `ASDCOREDBG`: core diagnostic generation.
- `ASD3`: clear the most recently generated plugin annotation group.
- `AG1`: special line-processing helper.

## Debug Flow

The repeatable four-direction regression is documented in `docs/DimensionLayoutRegression.md`. The helper below is a manual smoke-test flow and does not archive or validate all four directions.

Use the helper script to build the plugin, generate an AutoCAD `.scr` script, start AutoCAD, `NETLOAD` the compiled DLL, and run the configured test command:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-cad-test.ps1
```

The script settings are intentionally placed at the top of `scripts/run-cad-test.ps1`:

```powershell
$AcadExe = "D:\Program Files\Autodesk\AutoCAD 2020\acad.exe"
$DrawingPath = "C:\Users\Administrator\Desktop\test.dwg"
$ProjectPath = ""
$DllPath = ""
$DllCopyBaseName = "autofixdim"
$DllCopyVersionPrefix = "LB"
$DllCopyStartVersion = 61
$TestCommand = "ASD"
$Configuration = "Debug"
```

Set `$DrawingPath` to the DWG file AutoCAD should open before the generated `.scr` runs. Leave it empty to start AutoCAD without an explicit drawing file.

`$ProjectPath` can stay empty when there is only one `.csproj`, or when the script can uniquely identify the AutoCAD plugin project by AutoCAD references such as `AcMgd`, `AcDbMgd`, or `AcCoreMgd`. If multiple projects exist and the main plugin project cannot be identified, set `$ProjectPath` to the exact `.csproj`.

`$DllPath` can stay empty to use the project output DLL, such as `bin\Debug\AutoFixtureDim.dll`. Set it only when you want to load a copied or versioned test DLL.

When `$DllPath` is empty, the script copies `bin\Debug\AutoFixtureDim.dll` to the next available versioned DLL such as `bin\Debug\autofixdim-LB61.dll`, then loads that copied DLL in AutoCAD. Existing `autofixdim-LB*.dll` files are scanned so the next run increments the version number.

By default the script does not close AutoCAD. To request a restart flow:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\run-cad-test.ps1 -Restart
```

`-Restart` asks existing AutoCAD windows to close, then starts AutoCAD again. It does not force-kill AutoCAD if a save prompt or another modal dialog keeps the process alive.
