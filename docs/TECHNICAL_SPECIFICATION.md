# ТЕХНИЧЕСКОЕ ЗАДАНИЕ (ТЗ)
**Диспетчер пользовательских сессий**
*Версия документа: 1.4.0*
*Платформа: Windows 10/11 (x64) только*
*Исполняемый файл: `mh-ts-manager.exe`*
*Лицензия: Проприетарная / По соглашению*

---

> **ℹ️ Примечание:** Данный документ является источником требований и спецификаций для разработки. Пользовательская документация (README, справка) формируется на его основе.

---

## 📖 1. О проекте

**Диспетчер пользовательских сессий** — это десктопное приложение для администраторов и специалистов технической поддержки, предназначенное для централизованного просмотра, управления и удалённого взаимодействия с пользовательскими сессиями на компьютерах под управлением Windows.

> ✅ **Цель:** Повышение эффективности администрирования многопользовательских систем за счёт унифицированного интерфейса управления сессиями в стиле Windows 11.

### 📜 История версий документа

| Версия | Дата | Автор | Изменения |
|--------|------|-------|-----------|
| **1.0.0** | Апрель 2026 | Коваленко П. | Первоначальная версия ТЗ |
| **1.1.0** | Апрель 2026 | Коваленко П. | Добавлен формат ячейки «Пользователь», уточнены столбцы, статусы сессий |
| **1.2.0** | Апрель 2026 | Коваленко П. | Установлено имя исполняемого файла `mh-ts-manager.exe`, обновлены пути и структура |
| **1.3.0** | Апрель 2026 | Коваленко П. | Добавлены лучшие практики из mh-compressor-manager: безопасность, RAII, обработка ошибок, многопоточность, Git-воркфлоу |
| **1.4.0** | Апрель 2026 | Коваленко П. | Обновление до .NET 10, C# 14, удаление System.Text.Json |

---

## ✨ 2. Возможности

### 2.1. Основной функционал

| Функция | Описание | Приоритет |
|---------|----------|-----------|
| **🔍 Просмотр сессий** | Отображение списка активных сессий с колонками как в Диспетчере задач Windows 11 (вкладка «Пользователи»), включая скрытые столбцы | Высокий |
| **👤 Форматирование пользователя** | Ячейка «Пользователь»: `Логин — Полное имя (приложений: N)` + иконки 🔒/🖥️ при блокировке/хранителе экрана | Высокий |
| **🔗 Удалённое подключение** | Запуск `mstsc /shadow:<ID> /control /noConsentPrompt` по двойному клику или кнопке | Высокий |
| **📋 Управление сессиями** | Контекстное меню: Развернуть, Переключиться, Отключить, Выйти, Отправить сообщение | Высокий |
| **🪟 Список приложений** | При раскрытии сессии — отображение окон, видимых в Alt+Tab (не процессов) | Высокий |
| **🔐 Контроль привилегий** | Кнопка-замок для повышения прав через UAC, индикация режима администратора | Высокий |
| **🌐 Локализация** | Поддержка всех языковых пакетов Windows, автоматический fallback, RTL-поддержка | Средний |

### 2.2. Оптимизации и особенности

- **⚡ Асинхронный опрос сессий** — обновление списка каждые 5 сек без блокировки UI (`async/await`, `Task.Run`)
- **🎯 Точная фильтрация окон** — использование `EnumWindows` + `ProcessIdToSessionId` + проверка стилей окон для отображения только пользовательских приложений (Alt+Tab)
- **♻️ Идемпотентность команд** — проверка состояния перед выполнением действий (например, не отправлять `msg` в отключённую сессию)
- **🛡️ Защита от блокировки интерфейса** — все Win32-выносы в фоновые потоки, таймауты на внешние процессы
- **🔄 Сохранение состояния** — запоминание позиции окна, выбранного языка, состояния раскрытия сессий, конфигурации столбцов между запусками

---

## 📋 3. Требования

### 3.1. Системные требования

| Компонент | Требование | Примечание |
|-----------|------------|------------|
| **ОС** | Windows 10 (21H2+) / Windows 11 (22H2+) | Только x64, ARM64 не поддерживается |
| **.NET** | .NET 10 Desktop Runtime | Self-contained deployment опционально |
| **UI Framework** | WinUI 3 (предпочтительно) или WPF + Fluent UI | Fluent Design, Mica backdrop |
| **Права** | Запуск без прав: ограниченный функционал; Запуск с правами администратора: полный доступ | UAC-запрос по требованию |
| **ОЗУ** | ≤ 100 МБ (покой), ≤ 250 МБ (пик) | Зависит от количества сессий |
| **Диск** | ≤ 50 МБ для установки + логи | Логи ротируются, максимум 5 МБ на файл |
| **Исполняемый файл** | `mh-ts-manager.exe` | Основное имя процесса, используется в диспетчере задач, путях, манифесте |

### 3.2. Зависимости

> ⚠️ **Важно:** Все зависимости управляются через .NET и системные компоненты Windows. Сторонние NuGet-пакеты минимизированы.

| Компонент | Источник | Версия | Назначение |
|-----------|----------|--------|------------|
| **WinRT / WinUI** | Windows SDK 22621+ | Встроено | UI, Resource Manager |
| **wtsapi32.dll** | Система | Встроено | Перечисление сессий (WTS API) |
| **user32.dll / kernel32.dll** | Система | Встроено | Работа с окнами, процессами, сессиями |
| **mstsc.exe** | Система | Встроено | Удалённое подключение (RDP shadow) |
| **msg.exe** | Система | Встроено | Отправка сообщений в сессии |
| **psapi.dll** | Система | Встроено | Получение информации о процессах (память, CPU) |

---

## ⚙️ 4. Конфигурация

### 4.1. Файл конфигурации (опционально)

Путь: `%APPDATA%\mh-ts-manager\settings.json`

```json
{
  "general": {
    "language": "system",
    "theme": "system",
    "autoRefreshInterval": 5,
    "expandAppsByDefault": false
  },
  "ui": {
    "sessions": {
      "columns": {
        "visible": ["User", "Status", "CPU", "Memory", "Disk", "Network", "GPU", "GPUEngine"],
        "order": ["User", "Status", "CPU", "Memory", "Disk", "Network", "GPU", "GPUEngine", "SessionID", "SessionType", "ClientName", "NPU", "NPUEngine"],
        "widths": {}
      }
    }
  },
  "advanced": {
    "enableDebugLogging": false,
    "logRetentionDays": 7,
    "confirmDangerousActions": true
  }
}
```

### 4.2. Параметры конфигурации

| Параметр | Тип | По умолчанию | Описание |
|----------|-----|--------------|----------|
| `language` | string | `"system"` | Язык интерфейса: `system`, `en-US`, `ru-RU`, `de-DE`, и т.д. |
| `theme` | string | `"system"` | Тема оформления: `system`, `light`, `dark` |
| `autoRefreshInterval` | integer | `5` | Интервал автообновления списка сессий (сек), диапазон 1–60 |
| `expandAppsByDefault` | boolean | `false` | Раскрывать список приложений для новых сессий по умолчанию |
| `ui.sessions.columns.visible` | array | См. выше | Список видимых столбцов в таблице сессий |
| `ui.sessions.columns.order` | array | См. выше | Порядок столбцов (поддерживается drag-and-drop) |
| `enableDebugLogging` | boolean | `false` | Включить отладочное логирование (только для разработки) |
| `logRetentionDays` | integer | `7` | Срок хранения логов в днях |
| `confirmDangerousActions` | boolean | `true` | Запрашивать подтверждение для действий «Выйти», «Отключить» |

> ⚠️ **Важно:** Настройки, изменённые в интерфейсе, сохраняются в `settings.json`. Аргументы командной строки имеют приоритет над файлом конфигурации.

### 4.3. Аргументы командной строки

```
mh-ts-manager.exe [ОПЦИИ]
```

| Аргумент | Описание | Пример |
|----------|----------|--------|
| `--config <path>` | Путь к альтернативному файлу настроек | `--config "C:\custom\settings.json"` |
| `--language <code>` | Принудительный язык интерфейса | `--language ru-RU` |
| `--theme <mode>` | Принудительная тема | `--theme dark` |
| `--debug` | Включить отладочное логирование | `--debug` |
| `--help`, `-h` | Вывод справки | `--help` |
| `--version`, `-v` | Вывод версии | `--version` |

### 4.4. Примеры использования

```powershell
# Запуск с настройками по умолчанию
mh-ts-manager.exe

# Запуск с тёмной темой и русским интерфейсом
mh-ts-manager.exe --theme dark --language ru-RU

# Запуск с отладочным логированием для диагностики
mh-ts-manager.exe --debug

# Просмотр версии
mh-ts-manager.exe --version
```

---

## 💻 5. Интерфейс пользователя

### 5.1. Визуальный стиль

- **Дизайн-система:** Fluent Design (Windows 11)
- **Фон:** `Mica` / `MicaAlt` с автоматической адаптацией к системной теме
- **Шрифты:** `Segoe UI Variable` (системный), размеры по гайдлайнам Win11
- **Иконки:** Segoe Fluent Icons или `SymbolIcon` (WinUI)
- **Анимации:** Плавное раскрытие/сворачивание (`Expander`), подсветка выбора, индикаторы загрузки

### 5.2. Структура окна

```
┌─────────────────────────────────────────────────────────────┐
│ 🔒 Диспетчер пользовательских сессий              [─][□][×] │
├─────────────────────────────────────────────────────────────┤
│ Пользователи                              [🔓][Подк][✉][⋮] │
├─────────────────────────────────────────────────────────────┤
│ Пользователь          │ Статус │ ЦП │ Память │ Диск │ Сеть │
├─────────────────────────────────────────────────────────────┤
│ ▼ admin — Администратор (3) 🔒 │ Активна │ 12% │ 512 МБ │ ... │
│   ├─ 🪟 Проводник                                          │
│   ├─ 🪟 Терминал                                           │
│   └─ 🪟 Диспетчер задач                                    │
│                                                             │
│ ▶ user1 — Иванов Иван (1) 🖥️ │ Бездействие │ 2% │ 256 МБ │ ... │
│                                                             │
│ ▶ user2 — Петров Пётр (0) │ Отключена │ 0% │ 128 МБ │ ... │
└─────────────────────────────────────────────────────────────┘
```

### 5.3. Панель инструментов (Toolbar)

| Элемент | Расположение | Действие / Меню |
|---------|--------------|-----------------|
| **Заголовок** | Слева | Текст: `Пользователи` (ресурсный ключ `Toolbar.UsersTitle`) |
| **Кнопка-замок** 🔓/🔒 | Слева от меню `⋮` | Индикация прав: `🔓` = админ-режим, `🔒` = ограниченный. Клик на `🔒` → диалог повышения привилегий |
| **Подключить** | Справа | Эквивалент двойного клика по сессии |
| **Отправить сообщение** ✉ | Справа | Открытие диалога отправки `msg` в выбранную сессию |
| **Меню `⋮`** | Край справа | Выпадающее меню: `Развернуть`, `Переключиться`, `Отключить`, `Выйти`, `─`, `УЗ (оснастка)`, `УЗ (настройки)`, `─`, `О программе` |

### 5.4. Формат ячейки столбца «Пользователь» ⭐

Ячейка отображает **композитную строку** с дополнительной информацией и индикаторами:

```
<Login> — <FullName> (<AppCount>) [🔒] [🖥️]
```

| Компонент | Описание | Пример |
|-----------|----------|--------|
| **`<Login>`** | Имя учётной записи (SAM-имя или UPN) | `admin`, `ivanov`, `user@domain.local` |
| **` — `** | Разделитель: пробел + тире + пробел (U+2014 или U+002D) | ` — ` |
| **`<FullName>`** | Полное имя пользователя из учётной записи (поля `FullName` / `DisplayName`) | `Администратор`, `Иванов Иван` |
| **` (<AppCount>)`** | Число **приложений** (не процессов!), видимых в Alt+Tab для данной сессии, в круглых скобках | `(3)`, `(0)`, `(12)` |
| **`🔒`** (опционально) | Иконка замка: отображается, если сессия заблокирована (`WTSIsSessionLocked` или `GetSessionInfo + WinSta0`) | Показывается только при `SessionState.Locked` |
| **`🖥️`** (опционально) | Иконка хранителя экрана: отображается, если в сессии активен скринсейвер (проверка через `SPI_GETSCREENSAVERACTIVE` + `SPI_GETSCREENSAVERRUNNING` в контексте сессии) | Показывается только при активном скринсейвере |

> ⚠️ **Важно:** `AppCount` — это количество **окон приложений**, которые пользователь видит при переключении `Alt+Tab`, а не количество процессов. Подсчёт осуществляется через фильтрацию `EnumWindows` (см. раздел 7.2).

**Примеры отображения:**

| Сценарий | Отображение в ячейке |
|----------|---------------------|
| Активная сессия, 3 приложения | `admin — Администратор (3)` |
| Заблокированная сессия, 1 приложение | `user1 — Иванов Иван (1) 🔒` |
| Скринсейвер активен, 0 приложений | `guest — Гость (0) 🖥️` |
| Заблокирована + скринсейвер, 2 приложения | `user2 — Петров Пётр (2) 🔒🖥️` |
| Нет полного имени | `svc_account — (0)` |

### 5.5. Контекстное меню таблицы (правый клик по заголовку)

```
▶ Выбрать столбцы…
  ☑ Пользователь
  ☑ Состояние
  ☑ ЦП
  ☑ Память
  ☑ Диск
  ☑ Сеть
  ☑ GPU
  ☑ Движок GPU
  ☐ ID
  ☐ Сеанс
  ☐ Имя клиента
  ☐ NPU
  ☐ Движок NPU
─────────────────────
▶ Сбросить по умолчанию
```

### 5.6. Контекстное меню сессии (правый клик по строке)

```
▶ Развернуть / Свернуть
─────────────────────
🔗 Подключить
🔄 Переключиться
🔌 Отключить
🚪 Выйти
✉ Отправить сообщение...
```

> ℹ️ Пункты, требующие прав администратора (`Отключить`, `Выйти`, `Переключиться`), автоматически блокируются (disabled) в ограниченном режиме.

### 5.7. Раскрытие сессии: список приложений

При раскрытии сессии (`Expander`) отображается список **окон приложений**, видимых пользователю в `Alt+Tab`.

**Критерии включения окна в список:**
1. `GetWindowThreadProcessId(hwnd)` → `ProcessIdToSessionId(pid)` == ID целевой сессии
2. `IsWindowVisible(hwnd) == TRUE`
3. `(GetWindowLongPtr(hwnd, GWL_EXSTYLE) & WS_EX_TOOLWINDOW) == 0`
4. `GetWindowTextLength(hwnd) > 0` (есть заголовок)
5. Окно не является системным (фильтрация по классу окна: `#32770`, `Shell_TrayWnd`, `TaskManagerWindow` и т.п.)

**Отображение:** Иконка приложения (извлекается через `ExtractAssociatedIcon` или `SHGetFileInfo`) + заголовок окна.

---

## 🔤 6. Локализация (i18n/L10n)

### 6.1. Архитектура ресурсов

| Компонент | Решение |
|-----------|---------|
| **Формат** | `.resw` (WinUI 3) — предпочтительно; `.resx` (WPF) — альтернатива |
| **Загрузчик** | `Windows.ApplicationModel.Resources.ResourceManager` (автоматический fallback) |
| **XAML-привязка** | Атрибут `x:Uid` → автоматическая подстановка `Key.Property` |
| **Код-доступ** | DI-сервис `ILocalizationService.GetString(key, params object[] args)` |
| **Язык по умолчанию** | `ApplicationLanguages.PrimaryLanguageOverride` = `null` → системный язык |

### 6.2. Поддержка языков

| Уровень | Языки | Поведение |
|---------|-------|-----------|
| **Базовый набор** | `en-US`, `ru-RU`, `de-DE`, `fr-FR`, `es-ES`, `zh-CN`, `ja-JP`, `ko-KR`, `pt-BR`, `ar-SA` | Полные переводы всех строк |
| **Автоматический fallback** | Любой язык ОС | `Текущий` → `ru-RU` → `en-US` → `Invariant` (реализуется Windows Resource Manager) |
| **Ручной выбор** | Все доступные в системе | Переключатель в `О программе` → `Настройки` → `Язык интерфейса` |

### 6.3. Ключевые ресурсные строки (пример)

```xml
<!-- === Приложение === -->
<data name="App.WindowTitle" xml:space="preserve">
  <value>Диспетчер пользовательских сессий</value>
</data>
<data name="App.About.Title" xml:space="preserve">
  <value>О приложении</value>
</data>

<!-- === Toolbar === -->
<data name="Toolbar.UsersTitle" xml:space="preserve">
  <value>Пользователи</value>
</data>
<data name="Toolbar.Connect" xml:space="preserve">
  <value>Подключить</value>
</data>
<data name="Toolbar.SendMessage" xml:space="preserve">
  <value>Отправить сообщение</value>
</data>
<data name="Toolbar.AdminMode.Locked" xml:space="preserve">
  <value>Разблокировать режим системного администратора?</value>
</data>
<data name="Toolbar.AdminMode.Active" xml:space="preserve">
  <value>Режим системного администратора</value>
</data>
<data name="Toolbar.AdminMode.ElevateConfirm" xml:space="preserve">
  <value>Для выполнения операции требуются права администратора. Перезапустить приложение с повышенными привилегиями?</value>
</data>

<!-- === Статусы сессий (не процессов!) === -->
<data name="Status.SessionActive" xml:space="preserve">
  <value>Активна</value>
</data>
<data name="Status.SessionDisconnected" xml:space="preserve">
  <value>Отключена</value>
</data>
<data name="Status.SessionIdle" xml:space="preserve">
  <value>Бездействие</value>
</data>
<data name="Status.SessionLocked" xml:space="preserve">
  <value>Заблокирована</value>
</data>
<data name="Status.SessionUnavailable" xml:space="preserve">
  <value>Недоступна</value>
</data>

<!-- === Заголовки столбцов === -->
<data name="Column.User" xml:space="preserve">
  <value>Пользователь</value>
</data>
<data name="Column.Status" xml:space="preserve">
  <value>Состояние</value>
</data>
<data name="Column.CPU" xml:space="preserve">
  <value>ЦП</value>
</data>
<data name="Column.Memory" xml:space="preserve">
  <value>Память</value>
</data>
<data name="Column.Disk" xml:space="preserve">
  <value>Диск</value>
</data>
<data name="Column.Network" xml:space="preserve">
  <value>Сеть</value>
</data>
<data name="Column.GPU" xml:space="preserve">
  <value>GPU</value>
</data>
<data name="Column.GPUEngine" xml:space="preserve">
  <value>Движок GPU</value>
</data>
<data name="Column.SessionID" xml:space="preserve">
  <value>ID</value>
</data>
<data name="Column.SessionType" xml:space="preserve">
  <value>Сеанс</value>
</data>
<data name="Column.ClientName" xml:space="preserve">
  <value>Имя клиента</value>
</data>
<data name="Column.NPU" xml:space="preserve">
  <value>NPU</value>
</data>
<data name="Column.NPUEngine" xml:space="preserve">
  <value>Движок NPU</value>
</data>

<!-- === Формат ячейки пользователя === -->
<data name="UserCell.Format" xml:space="preserve">
  <value>{0} — {1} ({2})</value>
</data>
<data name="UserCell.AppsCount.Tooltip" xml:space="preserve">
  <value>Приложений в сессии: {0}</value>
</data>
<data name="UserCell.Locked.Tooltip" xml:space="preserve">
  <value>Сессия заблокирована</value>
</data>
<data name="UserCell.Screensaver.Tooltip" xml:space="preserve">
  <value>Активен хранитель экрана</value>
</data>

<!-- === Контекстное меню таблицы === -->
<data name="Table.Columns.Select" xml:space="preserve">
  <value>Выбрать столбцы…</value>
</data>
<data name="Table.Columns.Reset" xml:space="preserve">
  <value>Сбросить по умолчанию</value>
</data>

<!-- === Контекстное меню сессии === -->
<data name="ContextMenu.Expand" xml:space="preserve">
  <value>Развернуть</value>
</data>
<data name="ContextMenu.Collapse" xml:space="preserve">
  <value>Свернуть</value>
</data>
<data name="ContextMenu.Connect" xml:space="preserve">
  <value>Подключить</value>
</data>
<data name="ContextMenu.Switch" xml:space="preserve">
  <value>Переключиться</value>
</data>
<data name="ContextMenu.Disconnect" xml:space="preserve">
  <value>Отключить</value>
</data>
<data name="ContextMenu.Logoff" xml:space="preserve">
  <value>Выйти</value>
</data>
<data name="ContextMenu.SendMessage" xml:space="preserve">
  <value>Отправить сообщение...</value>
</data>
```

### 6.4. RTL-поддержка

- Не фиксировать `FlowDirection` в XAML — WinUI автоматически зеркалит layout для `ar-SA`, `he-IL`, `fa-IR`.
- Иконки со стрелками: использовать `SymbolIcon` (автоматически адаптируются) или применять `RotateTransform` для векторных иконок.
- Проверка вёрстки: тестировать на `ar-SA` с длинными строками.

### 6.5. Рабочий процесс переводов

1. **Экспорт:** Скрипт `_extract-resw.ps1` собирает все `.resw` в мастер-файл `Resources.master.resw`.
2. **Платформа:** Crowdin / Lokalise / Weblate (поддержка `.resw` → XLIFF).
3. **Псевдолокализация:** На этапе QA строки заменяются на `[!! en-US !!]` для выявления багов вёрстки.
4. **Валидация:** 
   - Проверка на отсутствующие ключи
   - Проверка плейсхолдеров `{0}`, `{1}` в `UserCell.Format`
   - Отсутствие дубликатов `x:Uid`
5. **CI/CD:** Автоматическая сборка языковых пакетов, валидация `.resw` перед мержем в `main`.

---

## 🔐 7. Безопасность и управление привилегиями

### 7.1. Модель прав доступа

| Режим | Права | Доступный функционал |
|-------|-------|---------------------|
| **Ограниченный** (запуск без UAC) | Текущий пользователь | Просмотр сессий текущего пользователя, подключение к своим сессиям |
| **Администратор** (после UAC) | Полный доступ | Все функции: управление чужими сессиями, `msg`, `tscon`, `lusrmgr.msc` |

### 7.2. Техническая реализация: подсчёт приложений и индикаторы

#### 🔢 Подсчёт приложений (Alt+Tab) в сессии

```csharp
public async Task<int> CountAltTabAppsAsync(int sessionId)
{
    return await Task.Run(() =>
    {
        int count = 0;
        EnumWindows((hwnd, lParam) =>
        {
            // 1. Принадлежит ли окно целевой сессии?
            if (GetWindowThreadProcessId(hwnd, out var pid) == 0) return true;
            if (ProcessIdToSessionId(pid, out var winSessionId) != 0) return true;
            if (winSessionId != sessionId) return true;

            // 2. Видимо ли окно?
            if (!IsWindowVisible(hwnd)) return true;

            // 3. Исключаем инструментальные окна (не показываются в Alt+Tab)
            var exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            if ((exStyle & WS_EX_TOOLWINDOW) != 0) return true;
            if ((exStyle & WS_EX_APPWINDOW) == 0 && (exStyle & WS_EX_TOOLWINDOW) != 0) return true;

            // 4. Есть ли заголовок?
            if (GetWindowTextLength(hwnd) == 0) return true;

            // 5. Исключаем системные окна
            var className = GetClassName(hwnd);
            if (className is "#32770" or "Shell_TrayWnd" or "TaskManagerWindow" or "WorkerW")
                return true;

            count++;
            return true;
        }, IntPtr.Zero);
        return count;
    });
}
```

#### 🔒 Определение заблокированной сессии

```csharp
public bool IsSessionLocked(int sessionId)
{
    // Вариант 1: WTSQuerySessionInformation + WTSInfoClass.WTSIsSessionLocked (Windows 10+)
    if (WTSQuerySessionInformation(IntPtr.Zero, sessionId, 
        WTSInfoClass.WTSIsSessionLocked, out var ptr, out var len))
    {
        var isLocked = Marshal.ReadByte(ptr) != 0;
        WTSFreeMemory(ptr);
        return isLocked;
    }
    // Вариант 2: эвристика через WinSta0 + GetProcessWindowStation (fallback)
    return false;
}
```

#### 🖥️ Определение активного хранителя экрана

```csharp
public bool IsScreensaverRunningInSession(int sessionId)
{
    // Требуется выполнение кода в контексте целевой сессии
    // Через WTSQueryUserToken + ImpersonateLoggedOnUser + SystemParametersInfo
    // SPI_GETSCREENSAVERRUNNING возвращает TRUE, если скринсейвер активен
    // Упрощённая реализация: пропустить для v1.0, добавить в v1.2
    return false;
}
```

### 7.3. Кнопка-замок: логика работы

```csharp
// Псевдокод проверки и повышения привилегий
if (!IsElevated)
{
    // Показать 🔒, tooltip = "Разблокировать режим системного администратора?"
    // При клике:
    var result = ShowDialog("Требуется повышение привилегий. Перезапустить с правами администратора?");
    if (result == Confirm)
    {
        // Сохранить состояние (язык, позиция окна, конфигурация столбцов) в %TEMP%\mh-ts-manager-state.json
        // Перезапустить процесс с "runas"
        var psi = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath, // mh-ts-manager.exe
            Arguments = "--elevated --state-file \"...\"",
            Verb = "runas", // Вызовет UAC-диалог
            UseShellExecute = true
        };
        Process.Start(psi);
        Application.Current.Shutdown(0);
    }
}
else
{
    // Показать 🔓, tooltip = "Режим системного администратора"
    // Загрузить состояние из файла, удалить временный файл
}
```

### 7.4. Защита от уязвимостей

- ✅ Запрет на выполнение произвольных команд: все внешние вызовы (`mstsc`, `msg`, `lusrmgr`) — через `ProcessStartInfo` с валидацией аргументов.
- ✅ Санитизация путей: использование `Path.GetFullPath()`, проверка на `..`, запрет на запуск из `%TEMP%`.
- ✅ Защита от symlink-атак: перед записью логов/настроек проверять, что целевой путь не является символической ссылкой на системный каталог.
- ✅ Минимизация поверхности атаки: не использовать `dynamic`, `Reflection.Emit`, `unsafe` без явной необходимости.

### 7.5. Логирование

- **Путь:** `%APPDATA%\mh-ts-manager\logs\`
- **Формат:** `mh-ts-manager-YYYYMMDD.log` (ротация по дате)
- **Макс. размер файла:** 5 МБ (старые файлы удаляются при превышении `logRetentionDays`)
- **Уровни:** `INFO`, `WARN`, `ERROR`, `DEBUG` (только при `--debug`)
- **Содержимое:** 
  - Время, уровень, компонент, сообщение
  - **Не включать:** имена пользователей, IP-адреса, содержимое сообщений, пароли
- **Кодировка:** UTF-8 без BOM

---

## 📁 8. Структура проекта

```
mh-ts-manager/
├── README.md                          ← Обзор для GitHub (пользователи)
├── LICENSE                            ← Лицензия
├── .gitignore
├── _build.ps1                         ← Сборка (Debug/Release)
├── _publish.ps1                       ← Публикация (MSIX / self-contained)
├── _translate.ps1                     ← Управление переводами
│
├── docs/                              ← Документация
│   ├── README.md                      ← Оглавление
│   ├── specification/
│   │   └── TECHNICAL_SPEC.md          ← Этот файл
│   └── development/
│       ├── RULES.md                   ← Правила кода (C# 12, async/await, DI)
│       ├── DEPLOY.md                  ← Сборка и публикация
│       └── QWEN.md                    ← Контекст для AI-помощников
│
├── src/
│   ├── mh-ts-manager.csproj           ← Проект WinUI 3 / WPF
│   ├── App.xaml / App.xaml.cs         ← Точка входа, DI-контейнер
│   ├── Views/
│   │   ├── MainWindow.xaml            ← Основное окно
│   │   ├── Controls/
│   │   │   ├── SessionItem.xaml       ← Элемент сессии (Expander)
│   │   │   ├── UserCellControl.xaml   ← Композитная ячейка "Пользователь"
│   │   │   └── AppWindowItem.xaml     ← Элемент окна приложения
│   │   └── Dialogs/
│   │       ├── ElevateDialog.xaml     ← Диалог повышения прав
│   │       └── AboutDialog.xaml       ← О программе
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           ← Логика списка сессий
│   │   ├── SessionViewModel.cs        ← Данные одной сессии
│   │   └── LocalizationService.cs     ← Обёртка над ResourceManager
│   ├── Services/
│   │   ├── WtsSessionService.cs       ← Обёртка над wtsapi32.dll
│   │   ├── WindowEnumeratorService.cs ← EnumWindows + фильтрация Alt+Tab
│   │   ├── SessionAppStateService.cs  ← Подсчёт приложений, блокировка, скринсейвер
│   │   ├── CommandExecutor.cs         ← Безопасный запуск внешних процессов
│   │   └── SettingsService.cs         ← Чтение/запись settings.json
│   └── Models/
│       ├── SessionInfo.cs             ← DTO сессии (включая WTSConnectState)
│       ├── AppWindowInfo.cs           ← DTO окна приложения
│       └── UserCellData.cs            ← Данные для композитной ячейки пользователя
│
├── Strings/                           ← Локализация
│   ├── en-US/Resources.resw
│   ├── ru-RU/Resources.resw
│   ├── de-DE/Resources.resw
│   └── ...
│
├── tests/
│   ├── UnitTests/                     ← xUnit / MSTest
│   │   ├── WtsSessionServiceTests.cs
│   │   ├── WindowFilterTests.cs
│   │   └── UserCellFormatTests.cs
│   └── UITests/                       ← WinAppDriver (опционально)
│
└── packaging/
    ├── manifest.msix                  ← Манифест MSIX
    └── appxmanifest.xml               ← Идентификатор: mh-ts-manager
```

---

## 🚀 9. Этапы разработки

1. **Прототипирование:** Макеты в Figma/Blend, утверждение UI-стиля (Fluent Design), валидация формата ячейки «Пользователь»
2. **Ядро сессий:** Реализация `WtsSessionService` (P/Invoke wtsapi32), перечисление, статусы, расширенная информация
3. **Ядро окон:** `WindowEnumeratorService` (EnumWindows, фильтрация Alt+Tab), `SessionAppStateService` (подсчёт приложений, блокировка, скринсейвер)
4. **Локализация:** Настройка `.resw`-структуры, DI-сервис, `x:Uid`-привязки, fallback-цепочка, ключи `UserCell.*`
5. **UI-реализация:** 
   - Вёрстка MainWindow, SessionItem, UserCellControl (композитная ячейка)
   - Анимации, Mica-фон, поддержка RTL
   - Таблица с виртуализацией, управление столбцами (drag-and-drop, «Выбрать столбцы»)
6. **Управление правами:** Кнопка-замок, UAC-перезапуск, сохранение состояния
7. **Интеграция команд:** `mstsc`, `msg`, `lusrmgr.msc`, контекстное меню, toolbar
8. **Настройки и логирование:** `SettingsService`, ротация логов, `--debug` режим
9. **Тестирование:**
   - Юнит-тесты: логика фильтрации окон, форматирование `UserCell`, парсинг аргументов
   - Интеграционные: запуск на чистом Win10/11, разные языки, DPI 100–200%, 50+ сессий
   - Безопасность: запуск без прав, попытка инъекции в аргументы
   - Псевдолокализация: проверка вёрстки на `[!! en !!]`, RTL-тесты
10. **Упаковка:** 
    - Вариант А: **MSIX** (рекомендуется) — автоматические обновления, песочница
    - Вариант Б: **Self-contained EXE** — для сценариев без Store
    - Подпись кода (сертификат), включение языковых пакетов

---

## ✅ 10. Критерии приемки

### 10.1. Функциональные

- [ ] Список сессий загружается за <1.5 сек, автообновление без мерцания
- [ ] Двойной клик / кнопка «Подключить» корректно запускают `mstsc` с параметрами
- [ ] Контекстное меню и toolbar содержат все пункты, работают без зависаний
- [ ] Раскрытие сессии показывает только окна (Alt+Tab), не процессы; фильтрация корректна
- [ ] Кнопка-замок: `🔒` → диалог → перезапуск с правами → `🔓`, функционал разблокирован
- [ ] При запуске без прав: приложение не падает, ограничивает функции, показывает понятные сообщения

### 10.2. Ячейка «Пользователь» ⭐

- [ ] Формат строки: `<Login> — <FullName> (<AppCount>)` соблюдается точно (пробел-тире-пробел)
- [ ] `AppCount` отображает количество **приложений** (окон в Alt+Tab), а не процессов
- [ ] Иконка 🔒 отображается только при заблокированной сессии (`WTSIsSessionLocked == true`)
- [ ] Иконка 🖥️ отображается только при активном хранителе экрана (если реализовано)
- [ ] При отсутствии `FullName` отображается только `Login — (N)`
- [ ] Tooltip при наведении на `(N)` показывает «Приложений в сессии: N»
- [ ] Tooltip при наведении на 🔒/🖥️ показывает описание состояния
- [ ] Ячейка корректно обрезается/переносится при нехватке места (`TextTrimming`, `TextWrapping`)

### 10.3. Таблица сессий

- [ ] Отображает все столбцы по умолчанию, как в Диспетчере задач Windows 11 (Пользователь, Состояние, ЦП, Память, Диск, Сеть, GPU, Движок GPU)
- [ ] Позволяет включить скрытые столбцы (ID, Сеанс, Имя клиента, NPU, Движок NPU) через «Выбрать столбцы»
- [ ] Поддерживает drag-and-drop заголовков для изменения порядка
- [ ] Сохраняет конфигурацию столбцов в `settings.json` и восстанавливает при перезапуске
- [ ] **В столбце «Состояние» для строки пользователя отображается состояние сессии** (Active/Disconnected/Idle/Locked), а не агрегированное состояние процессов
- [ ] Цветовая индикация статуса (🟢/🟡/🔴) соответствует типу состояния
- [ ] При наведении на иконку статуса показывается tooltip с полным описанием и временем изменения
- [ ] Агрегация ресурсов (ЦП/Память/Диск/Сеть) корректно суммирует метрики всех процессов сессии
- [ ] Таблица работает плавно при 50+ сессиях (виртуализация, отсутствие мерцания)

### 10.4. Интерфейс и локализация

- [ ] Визуальный стиль соответствует Fluent Design Win11 (Mica, Segoe UI Variable, CornerRadius=8)
- [ ] Поддержка светлой/тёмной темы, переключение в реальном времени
- [ ] 100% текстов вынесены в `.resw`, хардкод в коде и XAML отсутствует
- [ ] `x:Uid` маппятся корректно, нет дубликатов или пропущенных ключей
- [ ] Приложение корректно работает при `ar-SA` (RTL: зеркальный layout, иконки адаптированы)
- [ ] Длинные строки не ломают вёрстку (тест: `de-DE`, `fi-FI` с `TextTrimming`)
- [ ] Даты/числа форматируются по `CultureInfo.CurrentCulture`, статусы сессий переведены
- [ ] При отсутствии `.resw` для языка: fallback на `en-US`, приложение не падает
- [ ] Псевдолокализация пройдена без критичных багов вёрстки

### 10.5. Безопасность и стабильность

- [ ] Логи пишутся в `%APPDATA%\mh-ts-manager\logs\`, ротация по 5 МБ, без PII
- [ ] Все внешние вызовы (`mstsc`, `msg`) с валидацией аргументов и обработкой `Win32Exception`
- [ ] Нет утечек ресурсов: `SafeHandle` для P/Invoke, `CancellationToken` для фоновых задач
- [ ] Защита от зацикливания при UAC-перезапуске (флаг `--elevated`, проверка состояния)
- [ ] Приложение не блокирует UI при длительных операциях (опрос сессий, перечисление окон)

### 10.6. Идентификация и дистрибуция

- [ ] Исполняемый файл имеет имя `mh-ts-manager.exe`
- [ ] Заголовок окна, иконка, «О программе» и панель задач отображают `Диспетчер пользовательских сессий` (с учётом локали)
- [ ] Пути, реестр (`HKCU\Software\mh-ts-manager\Settings`), логи используют идентификатор `mh-ts-manager`
- [ ] MSIX-пакет: корректный `PackageIdentity.Name`, `DisplayName` (локализуемый), `Capabilities`
- [ ] Self-contained EXE: размер ≤ 100 МБ, запуск без установки, проверка .NET-рантайма при необходимости

---

## 👤 11. Правообладатель и контакты

| Параметр | Значение |
|----------|----------|
| **Проект** | Диспетчер пользовательских сессий |
| **Исполняемый файл** | `mh-ts-manager.exe` |
| **Версия ТЗ** | 1.2.0 |
| **Платформа** | Windows 10/11 (x64) только |
| **Стек** | C# 12 / .NET 8 / WinUI 3 (или WPF) |
| **Год** | 2026 |
| **Автор ТЗ** | Коваленко Павел |
| **Репозиторий** | `https://github.com/pavelvkovalenko/mh-ts-manager` |
| **Лицензия** | По соглашению / Проприетарная |

---

> 📌 **Примечание:** Программа разрабатывается **только для Windows**. Кроссплатформенная поддержка (Linux, macOS) не планируется и не рассматривается в рамках текущей версии.

---

## 🛡️ 12. Лучшие практики разработки

> Данный раздел основан на практиках проекта [mh-compressor-manager](https://github.com/pavelvkovalenko/mh-compressor-manager) и адаптирован для стека C# / .NET 8 / WinUI 3.

### 12.1. Язык комментариев и сообщений

| Компонент | Правило | Пример |
|-----------|---------|--------|
| **Комментарии в коде** | На **русском языке** | `// Получение списка активных сессий` |
| **Пользовательские сообщения** | Через систему локализации (`.resw`), по умолчанию — язык ОС | `ResourceLoader.GetString("Toolbar.Connect")` |
| **Логи** | На **английском языке** (единый формат для диагностики) | `Logger.Info("Session refresh completed. Count: {0}", count)` |
| **Имена переменных и функций** | На **английском языке** (`PascalCase` для методов, `_camelCase` для полей) | `GetSessionInfoAsync()`, `_sessionService` |
| **Doxygen/XML-комментарии** | На **русском языке** для публичных API | `/// <summary>Перечисляет пользовательские сессии.</summary>` |

```csharp
// ✅ ПРАВИЛЬНО: комментарий на русском, лог на английском
// Перечисление окон, видимых в Alt+Tab, для целевой сессии
Logger.Debug("Enumerating windows for session {0}", sessionId);

// ❌ НЕПРАВИЛЬНО: хардкод русского текста в логе
Logger.Info("Перечисление окон для сессии " + sessionId);
```

### 12.2. Безопасность

| Правило | Описание | Аналог в C# |
|---------|----------|-------------|
| **Запрет небезопасных вызовов** | Не использовать `Process.Start(string)` без валидации | Всегда использовать `ProcessStartInfo` с явным `FileName` и `Arguments` |
| **Защита от symlink-атак** | Проверка путей перед записью | `Path.GetFullPath()`, проверка `FileAttributes.ReparsePoint` |
| **Санитизация ввода** | Валидация всех внешних данных | `Regex.IsMatch()`, `string.IsNullOrWhiteSpace()`, maxLength-проверки |
| **Минимизация привилегий** | Запуск без прав администратора по умолчанию | Повышение через `runas` только по требованию |
| **Безопасное хранение** | Не хранить пароли, токены, PII в логах | Логи содержат только ID сессии, коды ошибок, метрики |

### 12.3. Управление ресурсами (RAII-аналоги в C#)

| Паттерн | C++ (mh-compressor-manager) | C# (mh-ts-manager) |
|---------|-----------------------------|-------------------|
| **Автоматическая очистка** | `std::unique_ptr` с custom deleter | `using var`, `SafeHandle`, `IDisposable` |
| **Защита от утечек** | RAII-обёртки для C API | `try-finally`, `CancellationToken` |
| **Явное владение** | `unique_ptr` (exclusive), `shared_ptr` (shared) | `readonly` поля, инъекция зависимостей |
| **Совместимость с native** | `.get()` для raw pointer | `GCHandle`, `Marshal.AllocHGlobal` + `SafeHandle` |

```csharp
// ✅ ПРАВИЛЬНО: using var для SafeHandle (автоматический WTSFreeMemory)
using var hServer = WTSOpenServer(serverName);
using var pSessionInfo = WTSEnumerateSessionsEx(hServer);

// ✅ ПРАВИЛЬНО: CancellationToken для отмены фоновых задач
using var cts = new CancellationTokenSource();
await Task.Run(() => EnumerateWindows(cts.Token), cts.Token);

// ❌ НЕПРАВИЛЬНО: ручное управление ресурсами без защиты
var ptr = WTSQuerySessionInformation(...);
// ... код может выбросить исключение до WTSFreeMemory(ptr)
WTSFreeMemory(ptr);
```

### 12.4. Обработка ошибок

| Правило | Описание |
|---------|----------|
| **Логи через единый сервис** | Все ошибки — через `ILogger`/`Logger` с уровнями `Error`, `Warning`, `Info`, `Debug` |
| **Сохранение контекста ошибки** | Включать ID сессии, имя функции, `HRESULT`/`Win32Exception.NativeErrorCode` |
| **Идемпотентность операций** | Проверка состояния перед действием (не отключать уже отключённую сессию) |
| **Не использовать исключения для потока управления** | `TryGetSession()` вместо `GetSession()` + `try-catch` |
| **Очистка при ошибке** | Удалять временные файлы, освобождать хендлы в `finally`/`using` |

```csharp
// ✅ ПРАВИЛЬНО: try-catch с контекстом и очисткой
public async Task<bool> DisconnectSessionAsync(int sessionId, CancellationToken ct)
{
    try
    {
        Logger.Info("Attempting to disconnect session {0}", sessionId);
        var state = await GetSessionStateAsync(sessionId, ct);
        if (state == WTSConnectState.Disconnected)
        {
            Logger.Info("Session {0} already disconnected, skipping", sessionId);
            return true; // Идемпотентность
        }
        return await ExecuteWtsOperationAsync(sessionId, WTSCommand.Disconnect, ct);
    }
    catch (Win32Exception ex) when (ex.NativeErrorCode == 5) // ACCESS_DENIED
    {
        Logger.Warning("Access denied for session {0}: {1}", sessionId, ex.Message);
        return false;
    }
    catch (OperationCanceledException)
    {
        Logger.Info("Session disconnect cancelled by user");
        throw;
    }
    catch (Exception ex)
    {
        Logger.Error("Unexpected error disconnecting session {0}: {1}", sessionId, ex.Message);
        return false;
    }
}
```

### 12.5. Многопоточность

| Правило | Описание |
|---------|----------|
| **`async/await` для I/O** | Все вызовы к Win32 API — через `Task.Run` с `CancellationToken` |
| **`lock` для защиты состояния** | `private readonly object _lock = new();` для чтения/записи общих данных |
| **`ReaderWriterLockSlim` при частом чтении** | Множество читателей (обновление UI), редкие записи (опрос сессий) |
| **Не блокировать UI-поток** | `ConfigureAwait(false)` в сервисах, `DispatcherQueue` для обновления UI |
| **Избегать detached задач** | Не использовать `Task.Run(() => ..., TaskCreationOptions.DetachedFromParent)` |
| **Переиспользование буферов** | `ArrayPool<T>` для временных массивов при перечислении окон |

```csharp
// ✅ ПРАВИЛЬНО: ReaderWriterLockSlim для кэша сессий
private readonly ReaderWriterLockSlim _cacheLock = new();
private readonly Dictionary<int, SessionInfo> _sessionCache = new();

public IReadOnlyList<SessionInfo> GetCachedSessions()
{
    _cacheLock.EnterReadLock();
    try { return _sessionCache.Values.ToList().AsReadOnly(); }
    finally { _cacheLock.ExitReadLock(); }
}

public async Task RefreshCacheAsync(CancellationToken ct)
{
    var freshSessions = await _wtsService.EnumerateSessionsAsync(ct);
    _cacheLock.EnterWriteLock();
    try { _sessionCache.Clear(); foreach (var s in freshSessions) _sessionCache[s.Id] = s; }
    finally { _cacheLock.ExitWriteLock(); }
}
```

### 12.6. Производительность

| Оптимизация | Описание |
|-------------|----------|
| **Виртуализация списков** | `ItemsSource` + `x:Load`/`x:Phase` для отложенной отрисовки |
| **Пакетное обновление UI** | Группировка изменений сессий, один `DispatcherQueue.TryEnqueue` |
| **Таймауты на внешние вызовы** | `CancellationTokenSource.Timeout` для `Process.WaitForExit`, `Task.WhenAny` |
| **Кэширование иконок** | `ImageSource` кэшируется по PID, не извлекается при каждом обновлении |
| **`IValueConverter` для форматирования** | Форматирование чисел (CPU%, Memory) через конвертеры, не в ViewModel |
| **`ArrayPool<T>` для EnumWindows** | Переиспользование буферов при перечислении окон |

### 12.7. Git-воркфлоу и Pull Request

> На основе практик mh-compressor-manager, адаптировано для Windows-разработки.

#### Ветки

| Правило | Формат | Пример |
|---------|--------|--------|
| **Основная ветка** | `main` | Защищена, требует PR |
| **Ветки фич** | `feature/описание` | `feature/column-drag-drop` |
| **Ветки исправлений** | `fix/описание` | `fix/session-state-aggregation` |
| **Ветки рефакторинга** | `refactor/описание` | `refactor/wts-service-di` |

#### Коммиты

- **Язык:** русский
- **Формат:** `[Тип]: Краткое описание (до 72 символов)`
- **Типы:** `fix:`, `chore:`, `feat:`, `refactor:`, `docs:`, `test:`, `perf:`

```
✅ feat: добавление drag-and-drop для столбцов таблицы
✅ fix: корректное определение заблокированной сессии
✅ docs: обновление технической спецификации v1.3
❌ "update", "fix bugs", "wip" — запрещены
```

#### Pull Request

- **Заголовок:** `[Тип]: Краткое описание на русском`
- **Описание:** подробное, на русском языке, структура:
  1. **Проблема** (симптомы, воспроизведение)
  2. **Решение** (затронутые файлы, подход)
  3. **Результат** (как проверить, скриншоты)
- **Тестирование:** раздел `## ✅ Тестирование` с чек-листом:
  - [ ] Сборка без ошибок
  - [ ] Все unit-тесты пройдены
  - [ ] Ручное тестирование на Windows 10/11

#### Запреты

- ❌ Прямая работа с `main` (только через PR)
- ❌ Force-push в `main`
- ❌ Мерж без пройденных тестов и ревью

---

## 📁 13. Структура проекта (обновлённая)

```
mh-ts-manager/
├── README.md                          ← Полный обзор проекта (пользователи + разработчики)
├── LICENSE                            ← Лицензия (MIT / Proprietary)
├── CONTRIBUTING.md                    ← Правила Pull Request, шаблон описания
├── .gitignore                         ← Исключения для C#, WinUI, VS, логов
├── _build.ps1                         ← Локальная сборка (Debug/Release)
├── _publish.ps1                       ← Публикация (MSIX / self-contained EXE)
│
├── docs/                              ← Документация
│   ├── specification/
│   │   └── TECHNICAL_SPECIFICATION.md ← Этот файл (ТЗ v1.3.0)
│   └── development/
│       ├── RULES.md                   ← Правила кода (C# 12, async/await, DI, x:Uid)
│       ├── DEPLOY.md                  ← Сборка, публикация, подпись кода
│       └── QWEN.md                    ← Контекст для AI-помощников
│
├── scripts/                           ← Скрипты автоматизации
│   ├── _build.ps1                     ← Сборка (dotnet build, msbuild)
│   ├── _publish.ps1                   ← Публикация (msix, self-contained)
│   └── _extract-resw.ps1              ← Экспорт ресурсных ключей для перевода
│
├── src/
│   ├── mh-ts-manager.csproj           ← Проект WinUI 3
│   ├── App.xaml / App.xaml.cs         ← Точка входа, DI-контейнер, настройка темы
│   ├── Views/
│   │   ├── MainWindow.xaml            ← Основное окно (Mica, Fluent Design)
│   │   ├── Controls/
│   │   │   ├── SessionItemControl.xaml ← Элемент сессии (Expander + таблица)
│   │   │   ├── UserCellControl.xaml   ← Композитная ячейка «Пользователь»
│   │   │   └── AppWindowItemControl.xaml ← Элемент окна приложения
│   │   └── Dialogs/
│   │       ├── ElevateDialog.xaml     ← Диалог повышения прав
│   │       └── AboutDialog.xaml       ← О программе / Настройки
│   ├── ViewModels/
│   │   ├── MainViewModel.cs           ← Логика списка сессий (CommunityToolkit.Mvvm)
│   │   ├── SessionViewModel.cs        ← Данные одной сессии
│   │   └── SettingsViewModel.cs       ← Настройки и локализация
│   ├── Services/
│   │   ├── WtsSessionService.cs       ← Обёртка над wtsapi32.dll (SafeHandle)
│   │   ├── WindowEnumeratorService.cs ← EnumWindows + фильтрация Alt+Tab
│   │   ├── SessionAppStateService.cs  ← Подсчёт приложений, блокировка, скринсейвер
│   │   ├── CommandExecutor.cs         ← Безопасный запуск mstsc, msg, lusrmgr
│   │   ├── SettingsService.cs         ← Чтение/запись settings.json (атомарная запись)
│   │   ├── LocalizationService.cs     ← Обёртка над ResourceManager
│   │   └── Logger.cs                  ← Логирование (файл + Debug)
│   └── Models/
│       ├── SessionInfo.cs             ← DTO сессии (record, immutable)
│       ├── AppWindowInfo.cs           ← DTO окна приложения (record)
│       └── UserCellData.cs            ← Данные для композитной ячейки (record)
│
├── Strings/                           ← Локализация (.resw)
│   ├── en-US/Resources.resw
│   ├── ru-RU/Resources.resw
│   └── ...                            ← Дополнительные языки
│
├── tests/
│   ├── UnitTests/                     ← xUnit / MSTest
│   │   ├── WtsSessionServiceTests.cs
│   │   ├── WindowFilterTests.cs
│   │   ├── UserCellFormatTests.cs
│   │   └── SettingsServiceTests.cs
│   └── IntegrationTests/              ← Ручные тесты на Win10/11
│       └── README.md                  ← Инструкция по запуску
│
└── packaging/
    ├── manifest.msix                  ← Манифест MSIX
    └── appxmanifest.xml               ← PackageIdentity: mh-ts-manager
```

---
