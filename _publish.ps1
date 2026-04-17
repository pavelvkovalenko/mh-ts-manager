# Обёртка для scripts\_publish.ps1
# Запуск из корня: .\_publish.ps1 [-Mode SelfContained] [-Configuration Release] [-Yes]
[CmdletBinding()]
param(
    [ValidateSet("SelfContained", "FrameworkDependent", "Msix")]
    [string]$Mode = "SelfContained",
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",
    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",
    [string]$OutputDir,
    [switch]$Sign,
    [string]$CertificatePath,
    [switch]$CreateDevCertificate,
    [switch]$Yes,
    [switch]$NoLogo
)

$scriptPath = Join-Path $PSScriptRoot "scripts\_publish.ps1"
if (Test-Path $scriptPath) {
    & $scriptPath -Mode $Mode -Configuration $Configuration -Runtime $Runtime @PSBoundParameters
}
else {
    Write-Error "Файл scripts\_publish.ps1 не найден"
    exit 1
}
