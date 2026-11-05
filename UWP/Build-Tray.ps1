$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'Instale o .NET Framework 4.8 para compilar o componente desktop.' }
$output = Join-Path $PSScriptRoot 'Tray\TouchpadPS4.Tray.exe'
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /utf8output "/win32icon:$(Join-Path $PSScriptRoot 'Assets\Touchpad.ico')" "/out:$output" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll (Join-Path $PSScriptRoot 'Tray\Engine.cs') (Join-Path $PSScriptRoot 'Tray\TrayApp.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o componente desktop.' }
Write-Host "Componente desktop compilado: $output"
