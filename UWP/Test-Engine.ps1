$ErrorActionPreference = 'Stop'
$folder = Join-Path $PSScriptRoot 'obj\EngineChecks'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$source = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'Tray\Engine.cs') -Raw
# Só na cópia de teste: registra eventos, sem injetar mouse no sistema.
$source = $source.Replace('[DllImport("user32.dll")]', '')
$source = $source.Replace('static extern uint SendInput(uint n, ref INPUT inp, int sz);', 'public static System.Collections.Generic.List<uint> Events = new System.Collections.Generic.List<uint>(); public static System.Collections.Generic.List<int> MoveX = new System.Collections.Generic.List<int>(); static uint SendInput(uint n, ref INPUT inp, int sz) { Events.Add(inp.u.mi.dwFlags); MoveX.Add(inp.u.mi.dx); return n; }')
$instrumented = Join-Path $folder 'Engine.cs'
Set-Content -LiteralPath $instrumented -Value $source -Encoding UTF8
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$output = Join-Path $folder 'EngineChecks.exe'
& $compiler /nologo /target:exe /platform:x64 "/out:$output" $instrumented (Join-Path $PSScriptRoot 'Tray\EngineChecks.cs')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar os testes.' }
& $output
if ($LASTEXITCODE -ne 0) { throw 'Falha nos testes do motor.' }
