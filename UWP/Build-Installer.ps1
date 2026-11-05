param([string]$CertificateThumbprint = '')
$ErrorActionPreference = 'Stop'
$vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
if (-not (Test-Path -LiteralPath $vswhere)) { throw 'Instale o Visual Studio com desenvolvimento UWP, ferramentas C# UWP e Windows SDK 10.0.19041.0. Veja README.md.' }
$msbuild = & $vswhere -latest -products '*' -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
if (-not $msbuild) { throw 'MSBuild do Visual Studio não encontrado.' }
$sdk = Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\Platforms\UAP\10.0.19041.0'
if (-not (Test-Path -LiteralPath $sdk)) { throw 'Instale o Windows SDK 10.0.19041.0 com suporte UWP.' }
$destination = Join-Path $PSScriptRoot 'Installer'
New-Item -ItemType Directory -Force -Path $destination | Out-Null
if ($CertificateThumbprint) {
    $certificate = Get-Item -LiteralPath "Cert:\CurrentUser\My\$CertificateThumbprint"
    if ($certificate.Subject -ne 'CN=TouchpadPS4') { throw 'O Subject do certificado deve corresponder ao Publisher do manifesto: CN=TouchpadPS4.' }
} else {
    $certificate = New-SelfSignedCertificate -Type Custom -Subject 'CN=TouchpadPS4' -FriendlyName 'Touchpad PS4 - distribuição local' -CertStoreLocation 'Cert:\CurrentUser\My' -KeyUsage DigitalSignature -KeyExportPolicy NonExportable -NotAfter (Get-Date).AddYears(2) -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3','2.5.29.19={text}')
}
& $msbuild (Join-Path $PSScriptRoot 'TouchpadPS4.csproj') /restore /t:Build /p:Configuration=Release /p:Platform=x64 /p:GenerateAppxPackageOnBuild=true /p:AppxBundle=Never /p:UapAppxPackageBuildMode=SideloadOnly "/p:AppxPackageDir=$destination\" /p:AppxPackageSigningEnabled=true "/p:PackageCertificateThumbprint=$($certificate.Thumbprint)" /p:AppxPackageSigningTimestampServerUrl=
if ($LASTEXITCODE -ne 0) { throw 'A geração do instalador falhou. Consulte a saída do MSBuild.' }
Export-Certificate -Cert $certificate -FilePath (Join-Path $destination 'TouchpadPS4.cer') | Out-Null
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Install.ps1') -Destination $destination -Force
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'Instalar.cmd') -Destination $destination -Force
Write-Host "Instalador gerado em $destination. Distribua a pasta inteira. O certificado local precisa ser confiado no computador de destino."
