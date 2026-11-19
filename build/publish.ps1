<#
.SYNOPSIS
    Produit les livrables de SafeVault dans le dossier artifacts\ :
      - SafeVault_<version>_x64.msix   : paquet d'installation signé (+ certificat .cer) ;
      - SafeVault-<version>-win-x64-portable.zip : version portable, sans installation.

.PARAMETER CertificateThumbprint
    Empreinte du certificat de signature (magasin CurrentUser\My). Son sujet doit être
    identique au Publisher du manifeste (CN=SafeVault).

.PARAMETER CreateTestCertificate
    Crée (ou réutilise) un certificat de test auto-signé « CN=SafeVault » dans CurrentUser\My.
    Un tel certificat convient pour un usage personnel ; une diffusion publique demande
    un certificat de signature de code délivré par une autorité reconnue.

.PARAMETER SkipTests
    Ne lance pas les tests avant la publication.

.EXAMPLE
    .\build\publish.ps1 -CreateTestCertificate
#>
[CmdletBinding()]
param(
    [string] $CertificateThumbprint,
    [switch] $CreateTestCertificate,
    [switch] $SkipTests
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$project = Join-Path $root 'src\SafeVault.App\SafeVault.App.csproj'
$artifacts = Join-Path $root 'artifacts'
$publisher = 'CN=SafeVault'

[xml] $manifest = Get-Content (Join-Path $root 'src\SafeVault.App\Package.appxmanifest')
$version = $manifest.Package.Identity.Version

function Invoke-Step([string] $title, [scriptblock] $action) {
    Write-Host "`n== $title" -ForegroundColor Cyan
    & $action
    if ($LASTEXITCODE -and $LASTEXITCODE -ne 0) { throw "Échec : $title (code $LASTEXITCODE)" }
}

# --- Certificat -------------------------------------------------------------
if ($CreateTestCertificate -and -not $CertificateThumbprint) {
    $cert = Get-ChildItem Cert:\CurrentUser\My |
        Where-Object { $_.Subject -eq $publisher -and $_.HasPrivateKey -and $_.NotAfter -gt (Get-Date) } |
        Select-Object -First 1
    if (-not $cert) {
        $cert = New-SelfSignedCertificate -Type Custom -Subject $publisher -KeyUsage DigitalSignature `
            -FriendlyName 'SafeVault - signature de test' -CertStoreLocation 'Cert:\CurrentUser\My' `
            -NotAfter (Get-Date).AddYears(2) `
            -TextExtension @('2.5.29.37={text}1.3.6.1.5.5.7.3.3', '2.5.29.19={text}')
        Write-Host "Certificat de test créé : $($cert.Thumbprint)"
    }
    $CertificateThumbprint = $cert.Thumbprint
}

if (-not $CertificateThumbprint) {
    throw 'Indiquez -CertificateThumbprint ou -CreateTestCertificate.'
}

# --- Tests ------------------------------------------------------------------
if (-not $SkipTests) {
    Invoke-Step 'Tests' { dotnet test (Join-Path $root 'tests\SafeVault.Tests') -c Release -nologo -v q }
}

Remove-Item $artifacts -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory $artifacts | Out-Null
$staging = Join-Path $artifacts 'staging'

# --- Paquet MSIX ------------------------------------------------------------
Invoke-Step "Paquet MSIX $version" {
    dotnet publish $project -c Release -nologo -v q `
        -p:WindowsPackageType=MSIX `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxBundle=Never `
        -p:AppxSymbolPackageEnabled=false `
        -p:AppxPackageSigningEnabled=true `
        -p:PackageCertificateThumbprint=$CertificateThumbprint `
        -p:AppxPackageDir="$staging\msix\\"
}

$msix = Get-ChildItem "$staging\msix" -Recurse -Filter *.msix | Select-Object -First 1
Copy-Item $msix.FullName (Join-Path $artifacts "SafeVault_${version}_x64.msix")
$cer = Join-Path $artifacts 'SafeVault.cer'
Export-Certificate -Cert "Cert:\CurrentUser\My\$CertificateThumbprint" -FilePath $cer | Out-Null

# --- Version portable -------------------------------------------------------
$portable = Join-Path $staging 'portable'
Invoke-Step 'Version portable' {
    dotnet publish $project -c Release -nologo -v q -p:WindowsPackageType=None -o $portable
}

Get-ChildItem $portable -Filter *.pdb -Recurse | Remove-Item
$zip = Join-Path $artifacts "SafeVault-$($version.Substring(0, $version.LastIndexOf('.')))-win-x64-portable.zip"
Compress-Archive -Path "$portable\*" -DestinationPath $zip

Remove-Item $staging -Recurse -Force

Write-Host "`nLivrables :" -ForegroundColor Green
Get-ChildItem $artifacts | ForEach-Object { '  {0,-45} {1,8:N1} Mo' -f $_.Name, ($_.Length / 1MB) }
