$ErrorActionPreference = "Stop"

try {
    $acad = [Runtime.InteropServices.Marshal]::GetActiveObject("AutoCAD.Application")
} catch {
    throw "No running AutoCAD.Application COM instance was found. Start AutoCAD first, then run this script again."
}

$document = $acad.ActiveDocument
if ($null -eq $document) {
    throw "AutoCAD is running, but there is no active document."
}

$document.SetVariable("FILEDIA", 1)
$document.SetVariable("CMDDIA", 1)

Write-Host "Restored AutoCAD dialog variables:"
Write-Host "FILEDIA = $($document.GetVariable('FILEDIA'))"
Write-Host "CMDDIA = $($document.GetVariable('CMDDIA'))"
