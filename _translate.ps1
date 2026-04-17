# Обёртка для scripts\_translate.ps1
# Запуск из корня: .\_translate.ps1 [-Action Extract|Check|Merge] [-Yes]
[CmdletBinding()]
param(
    [ValidateSet("Extract", "Check", "Merge")]
    [string]$Action = "Extract",
    [switch]$Yes
)

$scriptPath = Join-Path $PSScriptRoot "scripts\_translate.ps1"
if (Test-Path $scriptPath) {
    & $scriptPath -Action $Action @PSBoundParameters
}
else {
    Write-Error "Файл scripts\_translate.ps1 не найден"
    exit 1
}
