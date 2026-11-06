# Builds dist\PrayerTimes.exe using the C# compiler that ships with Windows (.NET Framework 4.x). No SDK needed.
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$csc  = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
$dist = Join-Path $root 'dist'
$exe  = Join-Path $dist 'PrayerTimes.exe'
$ico  = Join-Path $root 'app.ico'
New-Item -ItemType Directory -Force $dist | Out-Null

$common = @('/nologo', '/target:winexe', '/optimize+', '/codepage:65001',
            '/r:System.Device.dll', '/r:System.Windows.Forms.dll', '/r:System.Drawing.dll')
$src = Join-Path $root 'src\*.cs'

if (-not (Test-Path $ico)) {
    & $csc @common "/out:$exe" $src
    if ($LASTEXITCODE) { throw 'Compile failed' }
    Start-Process -FilePath $exe -ArgumentList '--export-icon', "`"$ico`"" -Wait
}

& $csc @common "/win32icon:$ico" "/out:$exe" $src
if ($LASTEXITCODE) { throw 'Compile failed' }
Write-Host "Built $exe"
