<#
.SYNOPSIS
    Скрипт публикации mh-ts-manager с установкой зависимостей
.DESCRIPTION
    Проверяет зависимости, запрашивает разрешение, устанавливает недостающие,
    затем публикует в выбранном режиме.
    Аналог _rpm-build.sh из mh-compressor-manager.
.PARAMETER Mode
    Режим публикации: SelfContained (по умолчанию), FrameworkDependent, Msix
.PARAMETER Configuration
    Конфигурация: Release (по умолчанию) или Debug
.PARAMETER Runtime
    Runtime identifier: win-x64 (по умолчанию)
.PARAMETER OutputDir
    Директория вывода
.PARAMETER Sign
    Подписать пакет
.PARAMETER CertificatePath
    Путь к сертификату
.PARAMETER CreateDevCertificate
    Создать самоподписанный сертификат
.PARAMETER Yes
    Автоматически соглашаться на установку зависимостей
#>

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("SelfContained", "FrameworkDependent", "Msix")]
    [string]$Mode = "SelfContained",

    [Parameter(Position = 1)]
    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [ValidateSet("win-x64", "win-arm64")]
    [string]$Runtime = "win-x64",

    [string]$OutputDir,
    [switch]$Sign,
    [string]$CertificatePath,
    [switch]$CreateDevCertificate,
    [switch]$Yes
)

# ============================================
# Настройки
# ============================================
$ProjectRoot = Split-Path -Parent $PSScriptRoot
$ProjectPath = Join-Path $ProjectRoot "src\mh-ts-manager.csproj"
$DotNetSdkVersion = "8.0"
$DotNetSdkInstallerUrl = "https://dot.net/v1/dotnet-install.ps1"

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
function Write-Info { param([string]$Text) Write-Host "  ℹ $Text" -ForegroundColor DarkGray }
function Write-Warn { param([string]$Text) Write-Host "  ⚠ $Text" -ForegroundColor DarkYellow }

# ============================================
# Запрос разрешения
# ============================================
function Request-InstallationPermission {
    param([string]$ComponentName, [string]$Description)

    if ($Yes) {
        Write-Info "Автоматическое согласие (-Yes): установка $ComponentName"
        return $true
    }

    Write-Host ""
    Write-Host "  ┌─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  │ Требуется установка: $ComponentName" -ForegroundColor White
    Write-Host "  │ $Description" -ForegroundColor DarkGray
    Write-Host "  ├─────────────────────────────────────────────────────────────" -ForegroundColor DarkGray
    Write-Host "  │ [Y] Да, установить" -ForegroundColor Green
    Write-Host "  │ [N] Нет, прервать" -ForegroundColor Red
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
        $version = (dotnet --version 2>&1).Trim()
        if ($LASTEXITCODE -eq 0 -and $version -match "^$DotNetSdkVersion\.\d+") {
            return @{ Found = $true; Version = $version }
        }
        return @{ Found = $false; Version = $version; Reason = "Требуется .NET $DotNetSdkVersion.x" }
    }
    catch {
        return @{ Found = $false; Version = $null; Reason = "dotnet не найден" }
    }
}

function Install-DotNetSdk {
    param([string]$Channel = "8.0")

    Write-Step "Загрузка установщика .NET SDK $Channel..."

    try {
        $installerPath = Join-Path $env:TEMP "dotnet-install.ps1"
        [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $DotNetSdkInstallerUrl -OutFile $installerPath -UseBasicParsing

        Write-Step "Установка .NET SDK $Channel (текущий пользователь)..."
        & $installerPath -Channel $Channel -InstallDir "$env:LOCALAPPDATA\Microsoft\dotnet" -NoPath -Verbose

        if ($LASTEXITCODE -ne 0) { throw "dotnet-install.ps1 error: $LASTEXITCODE" }

        $env:PATH = "$env:LOCALAPPDATA\Microsoft\dotnet;$env:PATH"
        Remove-Item $installerPath -Force -ErrorAction SilentlyContinue

        $newVersion = (dotnet --version 2>&1).Trim()
        Write-Success ".NET SDK $newVersion установлен"
        return $true
    }
    catch {
        Write-Error-Custom "Ошибка установки: $_"
        Write-Info "Скачайте вручную: https://dotnet.microsoft.com/download/dotnet/8.0"
        return $false
    }
}

# ============================================
# Создание сертификата
# ============================================
function New-DevCertificate {
    Write-Header "Создание самоподписанного сертификата"

    $certFile = Join-Path $ProjectRoot "dev-cert.pfx"
    $cerFile = Join-Path $ProjectRoot "dev-cert.cer"

    Write-Step "Создание сертификата для $AppName"

    try {
        $cert = New-SelfSignedCertificate `
            -Type Custom `
            -Subject "CN=mh-ts-manager Dev Certificate" `
            -KeyUsage DigitalSignature `
            -FriendlyName "mh-ts-manager Development" `
            -CertStoreLocation "Cert:\CurrentUser\My" `
            -TextExtension @("2.5.29.37={text}1.3.6.1.5.5.7.3.3", "2.5.29.19={text}")

        $securePassword = ConvertTo-SecureString -String "dev-password" -Force -AsPlainText
        Export-PfxCertificate -Cert $cert -FilePath $certFile -Password $securePassword | Out-Null
        Export-Certificate -Cert $cert -FilePath $cerFile | Out-Null

        Write-Success "Сертификат создан: $certFile"
        Write-Host "    Пароль: dev-password" -ForegroundColor Gray
        Write-Host "    Доверенный корень: Import-Certificate -FilePath '$cerFile' -CertStoreLocation 'Cert:\LocalMachine\Root'" -ForegroundColor Gray
    }
    catch {
        Write-Error-Custom "Ошибка создания сертификата: $_"
        exit 1
    }
}

# ============================================
# Подпись файла
# ============================================
function Sign-File {
    param([string]$FilePath, [string]$CertPath)

    if (-not (Test-Path $CertPath)) { Write-Error-Custom "Сертификат не найден: $CertPath"; return $false }

    Write-Step "Подпись: $FilePath"

    try {
        $signtool = Get-Command signtool -ErrorAction SilentlyContinue
        if (-not $signtool) {
            # Поиск в Windows Kits
            $kitsPath = "C:\Program Files (x86)\Windows Kits\10\bin"
            if (Test-Path $kitsPath) {
                $signtoolPath = Get-ChildItem $kitsPath -Filter "signtool.exe" -Recurse -ErrorAction SilentlyContinue | Select-Object -First 1
                if ($signtoolPath) { $signtool = $signtoolPath.FullName }
            }
        }
        else { $signtool = $signtoolPath.FullName }

        if ($signtool) {
            & $signtool sign /f $CertPath /p "dev-password" /fd SHA256 /tr http://timestamp.digicert.com /td SHA256 $FilePath
            Write-Success "Файл подписан"
            return $true
        }
        else {
            Write-Warn "signtool.exe не найден. Установите Windows SDK."
            return $false
        }
    }
    catch { Write-Error-Custom "Ошибка подписи: $_"; return $false }
}

# ============================================
# Проверка зависимостей
# ============================================
function Invoke-DependencyCheck {
    $dotnetCheck = Test-DotNetSdk
    if ($dotnetCheck.Found) {
        Write-Success ".NET SDK $($dotnetCheck.Version) обнаружен"
        return $true
    }

    Write-Warn ".NET SDK не найден: $($dotnetCheck.Reason)"

    $permission = Request-InstallationPermission `
        -ComponentName ".NET SDK $DotNetSdkVersion" `
        -Description "Официальный SDK от Microsoft. Установка для текущего пользователя."

    if ($permission) {
        return (Install-DotNetSdk)
    }
    else {
        Write-Error-Custom "Публикация невозможна без .NET SDK"
        return $false
    }
}

# ============================================
# Публикация
# ============================================
function Publish-SelfContained {
    $targetDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot "publish\$Runtime" }

    Write-Header "Публикация: Self-Contained ($Runtime)"
    Write-Host "  Директория: $targetDir" -ForegroundColor Gray

    Write-Step "Публикация..."
    dotnet publish $ProjectPath `
        -c $Configuration -r $Runtime --self-contained true `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -o $targetDir --verbosity minimal

    if ($LASTEXITCODE -ne 0) { Write-Error-Custom "Ошибка публикации (код: $LASTEXITCODE)"; exit 1 }

    $exeFile = Get-ChildItem -Path $targetDir -Filter "mh-ts-manager.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exeFile) {
        $sizeMB = [math]::Round($exeFile.Length / 1MB, 2)
        Write-Success "Публикация завершена"
        Write-Host "    Файл: $($exeFile.FullName) ($sizeMB МБ)" -ForegroundColor Gray
    }
}

function Publish-FrameworkDependent {
    $targetDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot "publish\framework-dependent" }

    Write-Header "Публикация: Framework-Dependent"
    Write-Host "  Директория: $targetDir" -ForegroundColor Gray

    Write-Step "Публикация..."
    dotnet publish $ProjectPath -c $Configuration -o $targetDir --verbosity minimal

    if ($LASTEXITCODE -ne 0) { Write-Error-Custom "Ошибка публикации (код: $LASTEXITCODE)"; exit 1 }

    Write-Success "Публикация завершена"
    Write-Host "    Требует: .NET 8 Desktop Runtime" -ForegroundColor Gray
}

function Publish-Msix {
    $targetDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot "packaging" }

    Write-Header "Публикация: MSIX"
    Write-Host "  Для полной MSIX сборки используйте:" -ForegroundColor Gray
    Write-Host "    1. Visual Studio: Project → Publish → Create App Packages" -ForegroundColor Gray
    Write-Host "    2. Или добавьте Windows Application Packaging Project в solution" -ForegroundColor Gray
}

# ============================================
# Основной процесс
# ============================================
if ($CreateDevCertificate) {
    New-DevCertificate
    exit 0
}

if (-not $NoLogo) {
    Write-Header "mh-ts-manager — Публикация ($Mode)"
    Write-Host "  Каталог: $ProjectRoot" -ForegroundColor Gray
    Write-Host "  Конфигурация: $Configuration | Runtime: $Runtime" -ForegroundColor Gray
    Write-Host ""
}

# Проверка зависимостей
$depsOk = Invoke-DependencyCheck
if (-not $depsOk) {
    Write-Host ""
    Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Red
    Write-Host "  Публикация прервана: зависимости не установлены" -ForegroundColor Red
    Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Red
    Write-Host ""
    exit 1
}

Write-Host ""

# Публикация
switch ($Mode) {
    "SelfContained" { Publish-SelfContained }
    "FrameworkDependent" { Publish-FrameworkDependent }
    "Msix" { Publish-Msix }
}

# Подпись
if ($Sign -and $CertificatePath) {
    Write-Host ""
    $targetDir = if ($OutputDir) { $OutputDir } else { Join-Path $ProjectRoot "publish\$Runtime" }
    $exeFile = Get-ChildItem -Path $targetDir -Filter "mh-ts-manager.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($exeFile) { Sign-File -FilePath $exeFile.FullName -CertPath $CertificatePath }
}

Write-Host ""
Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Green
Write-Host "  Публикация успешна! Режим: $Mode" -ForegroundColor Green
Write-Host "───────────────────────────────────────────────────────────" -ForegroundColor Green
Write-Host ""

exit 0
