# Обёртка для scripts\_build.ps1
# Запуск из корня проекта: .\_build.ps1 [-Configuration Release] [-Clean] [-Yes]
[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",
    [switch]$Clean,
    [switch]$NoLogo,
    [switch]$SkipDependencyCheck,
    [switch]$Yes
)

$scriptPath = Join-Path $PSScriptRoot "scripts\_build.ps1"
if (Test-Path $scriptPath) {
    & $scriptPath -Configuration $Configuration @PSBoundParameters
}
else {
    Write-Error "Файл scripts\_build.ps1 не найден"
    exit 1
}
