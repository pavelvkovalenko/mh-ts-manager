<#
.SYNOPSIS
    Скрипт локальной сборки mh-ts-manager с автоматической установкой зависимостей
.DESCRIPTION
    Проверяет все зависимости (.NET 10 SDK, NuGet), запрашивает разрешение пользователя,
    устанавливает недостающие компоненты, затем выполняет restore + build.
    Аналог _local-build.sh из mh-compressor-manager, но с интерактивной установкой.
.PARAMETER Configuration
    Конфигурация сборки: Debug (по умолчанию) или Release
.PARAMETER NoLogo
    Не выводить заголовок скрипта
.PARAMETER Clean
    Очистить предыдущие артефакты сборки перед build
.PARAMETER Yes
    Автоматически соглашаться на установку зависимостей (без запроса)
.PARAMETER SkipDependencyCheck
    Пропустить проверку зависимостей (только сборка)
.EXAMPLE
    .\_build.ps1
    .\_build.ps1 -Configuration Release
    .\_build.ps1 -Configuration Release -Clean
    .\_build.ps1 -Configuration Release -Yes
    .\_build.ps1 -Configuration Release -SkipDependencyCheck
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Debug",

    [switch]$NoLogo,
    [switch]$Clean,
    [switch]$Yes,
    [switch]$SkipDependencyCheck
)

# ============================================
# Настройки
# ============================================
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$SolutionPath = Join-Path $ProjectRoot "src\mh-ts-manager.sln"
$ProjectPath = Join-Path $ProjectRoot "src\mh-ts-manager.csproj"

# Зависимости
$DotNetSdkVersion = "10.0"
$DotNetSdkInstallerUrl = "https://dot.net/v1/dotnet-install.ps1"
$GitMinimumVersion = "2.30"

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

function Write-Step {
    param([string]$Text)
    Write-Host "  → $Text" -ForegroundColor Yellow
}

function Write-Success {
    param([string]$Text)
    Write-Host "  ✓ $Text" -ForegroundColor Green
}

function Write-Error-Custom {
    param([string]$Text)
    Write-Host "  ✗ $Text" -ForegroundColor Red
}

function Write-Info {
    param([string]$Text)
    Write-Host "  ℹ $Text" -ForegroundColor DarkGray
}

function Write-Warn {
    param([string]$Text)
    Write-Host "  ⚠ $Text" -ForegroundColor DarkYellow
}

# ============================================
# Функции запроса разрешения
# ============================================
function Request-InstallationPermission {
    param(
        [string]$ComponentName,
        [string]$Description
    )

    if ($Yes) {
        Write-Info "Автоматическое согласие (-Yes): установка $ComponentName разрешена"
        return $true
    }

    Write-Host ""
    Write-Host "  ┌─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  │ Требуется установка: $ComponentName" -ForegroundColor White
    Write-Host "  │ $Description" -ForegroundColor DarkGray
    Write-Host "  ├─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  │ [Y] Да, установить" -ForegroundColor Green
    Write-Host "  │ [N] Нет, прервать сборку" -ForegroundColor Red
    Write-Host "  └─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host ""

    $choice = Read-Host "  Продолжить? (Y/N)"

    return ($choice -match '^[YyДд]')
}

# ============================================
# Проверка и установка .NET SDK
# ============================================
function Test-DotNetSdk {
    try {
        $output = dotnet --version 2>&1
        if ($LASTEXITCODE -eq 0) {
            $version = $output.Trim()
            # Проверяем мажорную версию (8.x)
            if ($version -match "^$DotNetSdkVersion\.\d+") {
                return @{ Found = $true; Version = $version }
            }
            else {
                return @{ Found = $false; Version = $version; Reason = "Требуется .NET $DotNetSdkVersion.x, обнаружен $version" }
            }
        }
        return @{ Found = $false; Version = $null; Reason = "dotnet команда не найдена" }
    }
    catch {
        return @{ Found = $false; Version = $null; Reason = "Ошибка при проверке: $_" }
    }
}

function Install-DotNetSdk {
    param([string]$Channel = "10.0")

    Write-Step "Загрузка установщика .NET SDK $Channel..."

    try {
        # Скачиваем официальный dotnet-install.ps1
        $installerPath = Join-Path $env:TEMP "dotnet-install.ps1"

        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $DotNetSdkInstallerUrl -OutFile $installerPath -UseBasicParsing

        Write-Step "Установка .NET SDK $Channel (текущий пользователь)..."
        Write-Info "Путь: %LOCALAPPDATA%\Microsoft\dotnet"
        Write-Info "Это не требует прав администратора"

        & $installerPath -Channel $Channel -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet" -NoPath -Verbose

        if ($LASTEXITCODE -ne 0) {
            throw "dotnet-install.ps1 завершился с кодом $LASTEXITCODE"
        }

        # Добавляем dotnet в PATH текущей сессии
        $dotnetPath = "$env:LOCALAPPDATA\Microsoft\dotnet"
        $env:PATH = "$dotnetPath;$env:PATH"

        # Удаляем временный установщик
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue

        $newVersion = (dotnet --version 2>&1).Trim()
        Write-Success ".NET SDK $newVersion установлен успешно"
        return $true
    }
    catch {
        Write-Error-Custom "Ошибка установки .NET SDK: $_"
        Write-Host ""
        Write-Info "Альтернативная установка:"
        Write-Host "    1. Откройте: https://dotnet.microsoft.com/download/dotnet/10.0" -ForegroundColor Gray
        Write-Host "    2. Скачайте '.NET SDK 10.0.x' для Windows x64" -ForegroundColor Gray
        Write-Host "    3. Запустите установщик" -ForegroundColor Gray
        Write-Host ""
        return $false
    }
}

# ============================================
# Проверка и установка NuGet пакетов
# ============================================
function Test-NuGetPackages {
    param([string]$ProjectFilePath)

    Write-Step "Проверка NuGet пакетов..."

    try {
        $result = dotnet restore $ProjectFilePath --verbosity quiet --dry-run 2>&1
        # --dry-run не всегда поддерживается, используем обычный restore
        $result = dotnet restore $ProjectFilePath --verbosity quiet 2>&1
        return @{ Restored = ($LASTEXITCODE -eq 0); Output = $result }
    }
    catch {
        return @{ Restored = $false; Output = $_.Exception.Message }
    }
}

# ============================================
# Проверка файлов проекта
# ============================================
function Test-ProjectFiles {
    if (Test-Path $SolutionPath) {
        return @{ Found = $true; Path = $SolutionPath; Type = "solution" }
    }
    elseif (Test-Path $ProjectPath) {
        return @{ Found = $true; Path = $ProjectPath; Type = "project" }
    }
    else {
        return @{ Found = $false; Path = $null; Type = $null }
    }
}

# ============================================
# Проверка зависимостей (основная функция)
# ============================================
function Invoke-DependencyCheck {
    $allDepsOk = $true

    # 1. Проверка .NET SDK
    Write-Step "Проверка .NET SDK $DotNetSdkVersion..."
    $dotnetCheck = Test-DotNetSdk

    if ($dotnetCheck.Found) {
        Write-Success ".NET SDK $($dotnetCheck.Version) обнаружен"
    }
    else {
        Write-Warn ".NET SDK не найден: $($dotnetCheck.Reason)"

        $permission = Request-InstallationPermission `
            -ComponentName ".NET SDK $DotNetSdkVersion" `
            -Description "Официальный SDK от Microsoft. Установка для текущего пользователя, без прав администратора."

        if ($permission) {
            $installResult = Install-DotNetSdk -Channel $DotNetSdkVersion
            if (-not $installResult) {
                $allDepsOk = $false
            }
        }
        else {
            Write-Error-Custom "Сборка невозможна без .NET SDK $DotNetSdkVersion"
            $allDepsOk = $false
        }
    }

    # Проверяем ещё раз после установки
    if (-not (Test-DotNetSdk).Found) {
        Write-Error-Custom ".NET SDK по-прежнему недоступен. Прерываем."
        return $false
    }

    return $allDepsOk
}

# ============================================
# Основной процесс
# ============================================
if (-not $NoLogo) {
    Write-Header "mh-ts-manager — Сборка ($Configuration)"
}

Write-Host "  Каталог: $ProjectRoot" -ForegroundColor Gray
Write-Host "  Конфигурация: $Configuration" -ForegroundColor Gray
Write-Host ""

# Шаг 0: Проверка зависимостей (если не пропущена)
if (-not $SkipDependencyCheck) {
    $depsOk = Invoke-DependencyCheck
    if (-not $depsOk) {
        Write-Host ""
        Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Red
        Write-Host "  Сборка прервана: зависимости не установлены" -ForegroundColor Red
        Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Red
        Write-Host ""
        exit 1
    }
    Write-Host ""
}

# Шаг 1: Проверка файлов проекта
$projectCheck = Test-ProjectFiles
if (-not $projectCheck.Found) {
    Write-Error-Custom "Не найден ни solution (.sln), ни проект (.csproj) в src/"
    exit 1
}

$BuildTarget = $projectCheck.Path
Write-Step "Обнаружен $($projectCheck.Type): $($projectCheck.Path)"

# Шаг 2: Clean (если запрошен)
if ($Clean) {
    Write-Step "Очистка артефактов сборки..."
    dotnet clean $BuildTarget -c $Configuration --verbosity quiet

    $binDirs = Get-ChildItem -Path (Join-Path $ProjectRoot "src") -Directory -Recurse -ErrorAction SilentlyContinue |
               Where-Object { $_.Name -eq "bin" -or $_.Name -eq "obj" }
    foreach ($dir in $binDirs) {
        Remove-Item -Path $dir.FullName -Recurse -Force -ErrorAction SilentlyContinue
    }
    Write-Success "Очистка завершена"
    Write-Host ""
}

# Шаг 3: Restore зависимостей
Write-Step "Restore NuGet зависимостей..."
dotnet restore $BuildTarget --verbosity quiet
if ($LASTEXITCODE -ne 0) {
    Write-Error-Custom "Ошибка restore зависимостей (код: $LASTEXITCODE)"
    Write-Info "Попробуйте удалить папки src/bin и src/obj и запустить заново"
    exit 1
}
Write-Success "Restore завершён"
Write-Host ""

# Шаг 4: Build
Write-Step "Сборка ($Configuration)..."
dotnet build $BuildTarget -c $Configuration --no-restore --verbosity minimal
if ($LASTEXITCODE -ne 0) {
    Write-Error-Custom "Сборка завершилась с ошибками (код: $LASTEXITCODE)"
    exit 1
}
Write-Success "Сборка завершена успешно"
Write-Host ""

# Шаг 5: Итоговая информация
$OutputPath = Join-Path $ProjectRoot "src\bin\$Configuration\net10.0-windows"
if (Test-Path $OutputPath) {
    $exeFile = Get-ChildItem -Path $OutputPath -Filter "mh-ts-manager.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exeFile) {
        $sizeMB = [math]::Round($exeFile.Length / 1MB, 2)
        Write-Success "Исполняемый файл: $($exeFile.FullName)"
        Write-Host "    Размер: $sizeMB МБ" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Green
Write-Host "  Сборка успешна! Конфигурация: $Configuration" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Green
Write-Host ""

exit 0
