param([string]$OutputPath = (Join-Path $PSScriptRoot 'ScreenSnap.exe'))

$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$sources = @(Get-ChildItem -LiteralPath $PSScriptRoot -Filter '*.cs' -File | ForEach-Object FullName)
$options = @(
    '/nologo', '/target:winexe', '/platform:anycpu', '/optimize+', '/warnaserror+', '/codepage:65001',
    '/reference:System.Windows.Forms.dll,System.Drawing.dll,System.Xml.dll,System.Core.dll',
    "/win32manifest:$(Join-Path $PSScriptRoot 'ScreenSnap.manifest')",
    "/win32icon:$(Join-Path $PSScriptRoot 'ScreenSnap.ico')", "/out:$OutputPath"
)
& $compiler @options @sources
if ($LASTEXITCODE -ne 0) { throw "Compilation failed with exit code $LASTEXITCODE." }
