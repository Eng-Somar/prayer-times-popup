# Compiles and runs the unit tests in tests\ with the C# compiler that ships with Windows.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$out  = Join-Path $root 'dist\tests'
New-Item -ItemType Directory -Force $out | Out-Null
$exe  = Join-Path $out 'Tests.exe'

$resource = @()
$data = Join-Path $root 'data\adhkar.tsv'
if (Test-Path $data) { $resource = @("/resource:$data,adhkar.tsv") }

& $csc /nologo /target:exe /codepage:65001 /main:TestRunner /r:System.Device.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll @resource "/out:$exe" (Join-Path $root 'src\*.cs') (Join-Path $root 'tests\*.cs')
if ($LASTEXITCODE) { throw 'Test compile failed' }
[Console]::OutputEncoding = [Text.Encoding]::UTF8
& $exe
exit $LASTEXITCODE
