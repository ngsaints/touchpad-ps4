param(
  [switch]$Probe,
  [int]$Seconds = 0,
  [double]$Sensitivity = 1.0,
  [switch]$NoTap,
  [switch]$NoScroll,
  [int]$TapMs = 280,
  [int]$TapDist = 24,
  [int]$WheelSteps = 36,
  [string]$Log = ""
)
$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path

Add-Type -Path (Join-Path $here 'ds4tm.cs')

$dev = Get-PnpDevice -Class HIDClass -Status OK |
       Where-Object { $_.InstanceId -like 'HID\*054C*09CC*' } | Select-Object -First 1
if (-not $dev) { throw 'DualShock 4 nao encontrado. Confira a conexao Bluetooth.' }

$inst = $dev.InstanceId
$bt = $inst -like 'HID\{*'
$path = '\\?\' + ($inst -replace '\\', '#') + '#{4d1e55b2-f16f-11cf-88cb-001111000030}'
Write-Host "Dispositivo: $inst"
Write-Host "Transporte:  $(if ($bt) { 'Bluetooth' } else { 'USB' })"

$err = [Ds4]::Open($path)
if ($err -ne 0) { throw "Nao abriu o dispositivo (Win32 erro $err). Feche Steam/DS4Windows e tente de novo." }

[Ds4]::IsBt = $bt
[Ds4]::Sens = $Sensitivity
[Ds4]::TapEnabled = -not $NoTap
[Ds4]::ScrollEnabled = -not $NoScroll
[Ds4]::TapMs = $TapMs
[Ds4]::TapDist = $TapDist
[Ds4]::WheelSteps = $WheelSteps
[Ds4]::Init()

if ($bt) { Write-Host ([Ds4]::EnableFullReports()) }

try {
  if ($Probe) {
    if (-not $Log) { $Log = Join-Path $here 'ds4-probe.log' }
    Write-Host "Probe: gravando em $Log"
    $sec = if ($Seconds -gt 0) { $Seconds } else { 30 }
    [void][Ds4]::Run($sec * 1000, $true, $Log)
  } else {
    Write-Host "Mouse ativo. Mova o dedo no touchpad. Ctrl+C para sair."
    $ms = if ($Seconds -gt 0) { $Seconds * 1000 } else { -1 }
    [void][Ds4]::Run($ms, $false, $null)
  }
} finally { [Ds4]::Close() }
