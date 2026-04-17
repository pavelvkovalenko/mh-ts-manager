<#
.SYNOPSIS
    Скрипт управления переводами mh-ts-manager
.DESCRIPTION
    Извлечение, проверка и обновление .resw файлов локализации.
    Аналог _translate.sh из mh-compressor-manager.
.PARAMETER Action
    Действие: Extract (извлечь мастер-файл), Check (проверить ключи), Merge (обновить языки)
.PARAMETER Yes
    Автоматически соглашаться на действия (без запроса)
.EXAMPLE
    .\_translate.ps1 -Action Extract
    .\_translate.ps1 -Action Check
    .\_translate.ps1 -Action Merge -Yes
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("Extract", "Check", "Merge")]
    [string]$Action = "Extract",

    [switch]$Yes
)

# ============================================
# Настройки
# ============================================
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$StringsDir = Join-Path $ProjectRoot "Strings"
$MasterFile = Join-Path $StringsDir "Resources.master.resw"

# ============================================
# Функции вывода
# ============================================
function Write-Header {
    param([string]$Text)
    Write-Host ""
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "═══════════════════════════════════════════════════════" -ForegroundColor Cyan
    Write-Host ""
}

function Write-Step { param([string]$Text) Write-Host "  → $Text" -ForegroundColor Yellow }
function Write-Success { param([string]$Text) Write-Host "  ✓ $Text" -ForegroundColor Green }
function Write-Error-Custom { param([string]$Text) Write-Host "  ✗ $Text" -ForegroundColor Red }
function Write-Warn { param([string]$Text) Write-Host "  ⚠ $Text" -ForegroundColor DarkYellow }

# ============================================
# Запрос разрешения
# ============================================
function Request-Permission {
    param([string]$Message)

    if ($Yes) { return $true }

    Write-Host ""
    Write-Host "  $Message" -ForegroundColor White
    $choice = Read-Host "  Продолжить? (Y/N)"
    return ($choice -match '^[YyДд]')
}

# ============================================
# Парсинг .resw
# ============================================
function Get-ReswKeys {
    param([string]$FilePath)
    $keys = @{}
    if (-not (Test-Path $FilePath)) { return $keys }

    try {
        [xml]$xml = Get-Content $FilePath -Encoding UTF8
        foreach ($data in $xml.root.data) {
            $name = $data.name
            $value = $data.value.'#text' -as [string]
            if ([string]::IsNullOrWhiteSpace($value)) {
                $value = $data.value -as [string]
            }
            $keys[$name] = $value
        }
    }
    catch { Write-Warn "Ошибка парсинга $FilePath : $_" }
    return $keys
}

# ============================================
# Действия
# ============================================
function Export-MasterFile {
    if (-not (Test-Path $StringsDir)) {
        Write-Error-Custom "Директория Strings не найдена: $StringsDir"
        exit 1
    }

    Write-Header "Извлечение мастер-файла .resw"

    $reswFiles = Get-ChildItem -Path $StringsDir -Recurse -Filter "*.resw"
    if ($reswFiles.Count -eq 0) {
        Write-Warn "Файлы .resw не найдены в $StringsDir"
        exit 0
    }

    Write-Step "Найдено языковых файлов: $($reswFiles.Count)"

    $allKeys = @{}
    foreach ($file in $reswFiles) {
        $keys = Get-ReswKeys -FilePath $file.FullName
        foreach ($kvp in $keys.GetEnumerator()) {
            if (-not $allKeys.ContainsKey($kvp.Key)) {
                $allKeys[$kvp.Key] = $kvp.Value
            }
        }
    }

    Write-Step "Уникальных ключей: $($allKeys.Count)"

    # Создание XML
    $xml = New-Object xml
    $xmlDeclaration = $xml.CreateXmlDeclaration("1.0", "UTF-8", $null)
    $xml.AppendChild($xmlDeclaration) | Out-Null

    $root = $xml.CreateElement("root")
    $xml.AppendChild($root) | Out-Null

    $sortedKeys = $allKeys.GetEnumerator() | Sort-Object Key
    foreach ($kvp in $sortedKeys) {
        $data = $xml.CreateElement("data")
        $data.SetAttribute("name", $kvp.Key)
        $data.SetAttribute("xml:space", "preserve")

        $valueElem = $xml.CreateElement("value")
        $valueElem.InnerText = $kvp.Value
        $data.AppendChild($valueElem) | Out-Null
        $root.AppendChild($data) | Out-Null
    }

    $xml.Save($MasterFile)
    Write-Success "Мастер-файл сохранён: $MasterFile"
}

function Check-ReswFiles {
    if (-not (Test-Path $MasterFile)) {
        Write-Error-Custom "Мастер-файл не найден: $MasterFile"
        Write-Host "    Сначала: .\_translate.ps1 -Action Extract" -ForegroundColor Red
        exit 1
    }

    Write-Header "Проверка .resw файлов"

    $masterKeys = Get-ReswKeys -FilePath $MasterFile
    $reswFiles = Get-ChildItem -Path $StringsDir -Recurse -Filter "*.resw" |
                 Where-Object { $_.Name -ne "Resources.master.resw" }

    $hasErrors = $false
    $totalFiles = 0
    $totalMissing = 0

    foreach ($file in $reswFiles) {
        $totalFiles++
        $fileKeys = Get-ReswKeys -FilePath $file.FullName
        $missingKeys = @()

        foreach ($kvp in $masterKeys.GetEnumerator()) {
            if (-not $fileKeys.ContainsKey($kvp.Key)) {
                $missingKeys += $kvp.Key
            }
        }

        if ($missingKeys.Count -gt 0) {
            $hasErrors = $true
            $totalMissing += $missingKeys.Count
            Write-Warn "$($file.Name): отсутствует $($missingKeys.Count) ключ(ей)"
            foreach ($key in $missingKeys | Select-Object -First 5) {
                Write-Host "      - $key" -ForegroundColor Gray
            }
            if ($missingKeys.Count -gt 5) {
                Write-Host "      ... и ещё $($missingKeys.Count - 5)" -ForegroundColor Gray
            }
        }
        else {
            Write-Success "$($file.Name): все ключи на месте ($($fileKeys.Count))"
        }
    }

    Write-Host ""
    if ($hasErrors) {
        Write-Error-Custom "Проверка: $totalFiles файлов, $totalMissing отсутствующих ключей"
        exit 1
    }
    else {
        Write-Success "Проверка: $totalFiles файлов, все ключи на месте"
        exit 0
    }
}

function Merge-ReswFiles {
    if (-not (Test-Path $MasterFile)) {
        Write-Error-Custom "Мастер-файл не найден: $MasterFile"
        exit 1
    }

    Write-Header "Обновление .resw файлов из мастер-файла"

    $masterKeys = Get-ReswKeys -FilePath $MasterFile
    $reswFiles = Get-ChildItem -Path $StringsDir -Recurse -Filter "*.resw" |
                 Where-Object { $_.Name -ne "Resources.master.resw" }

    foreach ($file in $reswFiles) {
        Write-Step "Обновление: $($file.Name)"

        $fileKeys = Get-ReswKeys -FilePath $file.FullName
        $addedCount = 0

        foreach ($kvp in $masterKeys.GetEnumerator()) {
            if (-not $fileKeys.ContainsKey($kvp.Key)) {
                $fileKeys[$kvp.Key] = ""
                $addedCount++
            }
        }

        if ($addedCount -gt 0) {
            Write-Success "Добавлено $addedCount новых ключей в $($file.Name)"
        }
        else {
            Write-Success "$($file.Name): обновлений не требуется"
        }
    }
}

# ============================================
# Основной процесс
# ============================================
switch ($Action) {
    "Extract" { Export-MasterFile }
    "Check" { Check-ReswFiles }
    "Merge" { Merge-ReswFiles }
}
