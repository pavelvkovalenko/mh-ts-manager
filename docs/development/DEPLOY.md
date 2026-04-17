# Сборка и публикация — mh-ts-manager

> Процедуры сборки, публикации и установки для Windows 10/11.

---

## 🔨 1. Локальная сборка

### 1.1. Предварительные требования

| Компонент | Требование |
|-----------|------------|
| **ОС** | Windows 10 21H2+ / Windows 11 22H2+ |
| **SDK** | .NET 8 SDK (8.0.x) — **установится автоматически** |
| **IDE** | Visual Studio 2022 17.8+ с workload «Windows App SDK (WinUI 3)» |
| **Windows App SDK** | 1.5+ (устанавливается через Visual Studio Installer) |

> 💡 **Автоматическая установка зависимостей:** Скрипт `_build.ps1` проверяет наличие .NET 8 SDK. Если он не найден — скрипт **запросит разрешение** и установит его автоматически через официальный `dotnet-install.ps1`. Установка для текущего пользователя, **без прав администратора**.

### 1.2. Сборка через PowerShell

```powershell
# Клонирование репозитория
git clone https://github.com/pavelvkovalenko/mh-ts-manager.git
cd mh-ts-manager

# Сборка Debug — скрипт проверит и установит зависимости
.\_build.ps1 -Configuration Debug

# Сборка Release с автоматическим согласием на установку
.\_build.ps1 -Configuration Release -Yes

# Сборка с очисткой предыдущих артефактов
.\_build.ps1 -Configuration Release -Clean

# Только сборка, пропустить проверку зависимостей
.\_build.ps1 -Configuration Release -SkipDependencyCheck
```

### 1.3. Сборка через dotnet CLI

```powershell
# Restore + Build
dotnet restore src/mh-ts-manager.csproj
dotnet build src/mh-ts-manager.csproj -c Release

# Запуск
dotnet run --project src/mh-ts-manager.csproj
```

### 1.4. Сборка через Visual Studio

1. Открыть `src/mh-ts-manager.sln`
2. Выбрать конфигуцию `Debug` / `Release`
3. `Build → Build Solution` (Ctrl+Shift+B)
4. Запуск: `Debug → Start Debugging` (F5) или `Start Without Debugging` (Ctrl+F5)

---

## 📦 2. Публикация

### 2.1. Self-contained EXE

Создаёт автономный исполняемый файл со встроенным .NET Runtime:

```powershell
.\_publish.ps1 -Mode SelfContained -Runtime win-x64 -Configuration Release
```

**Результат:** `publish/win-x64/mh-ts-manager.exe`

**Размер:** ~80-100 МБ (включает .NET Runtime)

**Запуск:** Не требует установленного .NET Runtime

### 2.2. Framework-dependent

Создаёт приложение, требующее установленный .NET Runtime:

```powershell
.\_publish.ps1 -Mode FrameworkDependent -Configuration Release
```

**Результат:** `publish/framework-dependent/`

**Размер:** ~5-10 МБ

**Требование:** `.NET 8 Desktop Runtime` установлен

### 2.3. MSIX пакет (рекомендуется)

Создаёт установочный пакет для Windows:

```powershell
.\_publish.ps1 -Mode Msix -Configuration Release
```

**Результат:** `packaging/mh-ts-manager_1.0.0.0_x64.msix`

**Установка:**
```powershell
Add-AppxPackage -Path packaging\mh-ts-manager_1.0.0.0_x64.msix
```

**Преимущества:**
- Песочница (AppContainer)
- Автоматические обновления
- Чистое удаление через `Settings → Apps`
- Корректная регистрация в Start Menu

### 2.4. Подпись кода

Для MSIX и self-contained EXE требуется подпись:

```powershell
# Создание самоподписанного сертификата (для разработки)
.\_publish.ps1 -CreateDevCertificate

# Подпись пакета
.\_publish.ps1 -Mode Msix -Sign -CertificatePath dev-cert.pfx
```

Для продакшена использовать сертификат от доверенного CA (DigiCert, Sectigo, etc.)

---

## 🚀 3. Развёртывание

### 3.1. Self-contained EXE — ручное развёртывание

```powershell
# 1. Распаковать в целевую директорию
Copy-Item -Path publish\win-x64\* -Destination "C:\Program Files\mh-ts-manager\" -Recurse

# 2. Создать ярлык
$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut("$env:USERPROFILE\Desktop\Диспетчер сессий.lnk")
$shortcut.TargetPath = "C:\Program Files\mh-ts-manager\mh-ts-manager.exe"
$shortcut.WorkingDirectory = "C:\Program Files\mh-ts-manager"
$shortcut.Save()

# 3. (Опционально) Добавить в автозагрузку
New-ItemProperty -Path "HKCU:\Software\Microsoft\Windows\CurrentVersion\Run" `
    -Name "MhTsManager" -Value "C:\Program Files\mh-ts-manager\mh-ts-manager.exe" -PropertyType String
```

### 3.2. MSIX — установка через PowerShell

```powershell
# Установка (требует Developer Mode или доверенный сертификат)
Add-AppxPackage -Path .\mh-ts-manager_1.0.0.0_x64.msix

# Проверка
Get-AppxPackage -Name "*mh-ts-manager*"

# Удаление
Remove-AppxPackage -Package (Get-AppxPackage -Name "*mh-ts-manager*").PackageFullName
```

### 3.3. Group Policy / SCCM (корпоративное развёртывание)

1. Подписать MSIX доверенным сертификатом
2. Развернуть через GPO: `Computer Configuration → Policies → Software Settings → Windows Settings → Assigned Access`
3. Или через SCCM: `Applications → Create Application → Windows app package (.msix)`

---

## 🔧 4. Процедуры обновления

### 4.1. Self-contained EXE

```powershell
# 1. Остановить работающие экземпляры
Get-Process mh-ts-manager -ErrorAction SilentlyContinue | Stop-Process -Force

# 2. Создать бэкап
Copy-Item -Path "C:\Program Files\mh-ts-manager" -Destination "C:\Program Files\mh-ts-manager.bak" -Recurse

# 3. Распаковать новую версию
Copy-Item -Path publish\win-x64\* -Destination "C:\Program Files\mh-ts-manager\" -Recurse -Force

# 4. Удалить бэкап
Remove-Item -Path "C:\Program Files\mh-ts-manager.bak" -Recurse -Force
```

### 4.2. MSIX

MSIX поддерживает автоматическое обновление через Windows Update или sideload-канал. Для ручной установки новой версии:

```powershell
# Установка поверх (same PackageIdentity, higher version)
Add-AppxPackage -Path .\mh-ts-manager_1.1.0.0_x64.msix
```

---

## 📋 5. Чек-лист релиза

Перед публикацией версии:

- [ ] Все unit-тесты пройдены (`dotnet test`)
- [ ] Сборка без ошибок (`dotnet build -c Release`)
- [ ] Анализаторы без предупреждений (`dotnet build -c Release /p:TreatWarningsAsErrors=true`)
- [ ] Локализация: все `.resw` файлы полные, нет пропущенных ключей
- [ ] Версия в `.csproj` обновлена (`<Version>1.3.0</Version>`)
- [ ] `CHANGELOG.md` обновлён (если существует)
- [ ] Тестирование на чистой Windows 10 21H2
- [ ] Тестирование на чистой Windows 11 22H2
- [ ] Тестирование с разными языками ОС (`ru-RU`, `en-US`, `ar-SA`)
- [ ] Тестирование в ограниченном режиме (без прав администратора)
- [ ] Тестирование в режиме администратора
- [ ] Размер self-contained EXE ≤ 100 МБ
- [ ] Логи пишутся в `%APPDATA%\mh-ts-manager\logs\`, без PII

---

## 🐛 6. Диагностика проблем

### 6.1. Сборка не выполняется

```powershell
# Очистка кэша
dotnet clean
Remove-Item -Recurse -Force src\bin, src\obj -ErrorAction SilentlyContinue

# Restore с подробностями
dotnet restore -v detailed

# Проверка SDK
dotnet --list-sdks
dotnet --list-runtimes
```

### 6.2. Приложение не запускается

```powershell
# Проверка .NET Runtime
dotnet --list-runtimes

# Запуск с --debug
.\mh-ts-manager.exe --debug

# Проверка логов
Get-Content "$env:APPDATA\mh-ts-manager\logs\*.log" -Tail 50

# Event Viewer
eventvwr.msc → Windows Logs → Application
```

### 6.3. MSIX не устанавливается

```powershell
# Проверка Developer Mode
Get-ItemProperty "HKLM:\SOFTWARE\Microsoft\Windows\CurrentVersion\AppModelUnlock" -Name "AllowDevelopmentWithoutDevLicense"

# Включение Developer Mode
# Settings → Privacy & Security → For developers → Developer Mode

# Проверка сертификата
Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -like "*mh-ts-manager*" }
```
