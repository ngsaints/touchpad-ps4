$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$output = Join-Path $PSScriptRoot 'bin'
New-Item -ItemType Directory -Force -Path $output | Out-Null
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $framework 'csc.exe') /nologo /target:winexe /platform:x64 /optimize+ /main:ModernProgram "/out:$output\TouchpadPS4.exe" "/win32manifest:$PSScriptRoot\app.manifest" "/win32icon:$root\UWP\Assets\Touchpad.ico" /reference:System.dll /reference:System.Core.dll /reference:System.Xaml.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Management.dll "/reference:$wpf\WindowsBase.dll" "/reference:$wpf\PresentationCore.dll" "/reference:$wpf\PresentationFramework.dll" (Join-Path $PSScriptRoot 'Program.cs') (Join-Path $root 'UWP\Tray\Engine.cs') (Join-Path $root 'UWP\Tray\TrayApp.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar a interface moderna.' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'MainWindow.xaml') -Destination $output -Force
Copy-Item -LiteralPath (Join-Path $root 'assets\dualshock4.png') -Destination $output -Force
Write-Host "Aplicativo compilado: $output\TouchpadPS4.exe"
