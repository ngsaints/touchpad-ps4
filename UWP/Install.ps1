param([switch]$TrustCertificate)
$ErrorActionPreference = 'Stop'
$packages = @(Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File | Where-Object { $_.Extension -in '.appx','.msix' -and $_.BaseName -like 'DaniSaints.TouchpadPS4*' })
if ($packages.Count -ne 1) { throw 'Esperado um único pacote Touchpad PS4. Use uma pasta de distribuição contendo somente a versão desejada.' }
$package = $packages[0]
if ($TrustCertificate) {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    if (-not $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) { throw 'Para confiar no certificado local, execute Instalar.cmd como administrador.' }
    $certPath = Join-Path $PSScriptRoot 'TouchpadPS4.cer'
    $certificate = New-Object Security.Cryptography.X509Certificates.X509Certificate2($certPath)
    $signature = Get-AuthenticodeSignature -LiteralPath $package.FullName
    if (-not $signature.SignerCertificate -or $signature.SignerCertificate.Thumbprint -ne $certificate.Thumbprint) { throw 'O certificado não corresponde à assinatura do pacote.' }
    Write-Host "Certificado: $($certificate.Subject)"
    Write-Host "Impressão digital: $($certificate.Thumbprint)"
    $answer = Read-Host 'Confiar neste certificado para instalar o Touchpad PS4? Digite SIM'
    if ($answer -ne 'SIM') { throw 'Instalação cancelada.' }
    Import-Certificate -FilePath $certPath -CertStoreLocation 'Cert:\LocalMachine\TrustedPeople' | Out-Null
}
$dependencies = @(Get-ChildItem -LiteralPath $package.DirectoryName -Recurse -Filter '*.appx' | Where-Object { $_.FullName -match '\\Dependencies\\(x64|neutral)\\' } | Select-Object -ExpandProperty FullName)
if ($dependencies.Count) { Add-AppxPackage -Path $package.FullName -DependencyPath $dependencies }
else { Add-AppxPackage -Path $package.FullName }
Write-Host 'Touchpad PS4 instalado. Abra pelo menu Iniciar.'
