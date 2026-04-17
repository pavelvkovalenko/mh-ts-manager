# Правила написания кода — mh-ts-manager

> Данный документ основан на практиках проекта [mh-compressor-manager](https://github.com/pavelvkovalenko/mh-compressor-manager) и адаптирован для стека C# 12 / .NET 8 / WinUI 3.

---

## 📋 1. Язык и стандарт

| Компонент | Правило |
|-----------|---------|
| **Язык** | C# 12 |
| **Фреймворк** | .NET 8 (NET8.0 target framework) |
| **UI Framework** | WinUI 3 (Windows App SDK 1.5+) — предпочтительно; WPF + CommunityToolkit.Mvvm — альтернатива |
| **Nullability** | `<Nullable>enable</Nullable>` обязательно |
| **Implicit usings** | `<ImplicitUsings>enable</ImplicitUsings>` |
| **Treat warnings as errors** | `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` |

---

## 🌐 2. Язык комментариев и сообщений

| Компонент | Правило | Пример |
|-----------|---------|--------|
| **Комментарии в коде** | На **русском языке** | `// Получение списка активных сессий` |
| **Логи** | На **английском языке** | `Logger.Debug("Session refresh completed. Count: {0}", count)` |
| **Пользовательские сообщения** | Через `.resw` (ResourceManager), **без хардкода** | `ResourceLoader.GetString("Toolbar.Connect")` |
| **XML-документация** | На **русском языке** | `/// <summary>Перечисляет сессии.</summary>` |
| **Имена переменных/методов** | На **английском языке** | `GetSessionInfoAsync()`, `_sessionService` |

```csharp
// ✅ ПРАВИЛЬНО: комментарий на русском, лог на английском, сообщения через ресурсы
// Перечисление окон, видимых в Alt+Tab, для целевой сессии
Logger.Debug("Enumerating windows for session {0}", sessionId);
var title = ResourceLoader.GetForCurrentView("Resources").GetString("App.WindowTitle");

// ❌ НЕПРАВИЛЬНО: хардкод русского текста в логе и UI
Logger.Info("Перечисление окон для сессии " + sessionId);
WindowTitle = "Диспетчер пользовательских сессий";
```

---

## 🔤 3. Именование

### 3.1. Общие правила

| Элемент | Стиль | Пример |
|---------|-------|--------|
| **Классы** | `PascalCase` | `SessionViewModel`, `WtsSessionService` |
| **Методы** | `PascalCase` | `GetSessionInfoAsync`, `RefreshCache` |
| **Публичные свойства** | `PascalCase` | `SessionId`, `UserName` |
| **Приватные поля** | `_camelCase` | `_sessionService`, `_cancellationToken` |
| **Локальные переменные** | `camelCase` | `sessionId`, `windowHandle` |
| **Параметры методов** | `camelCase` | `cancellationToken`, `sessionId` |
| **Константы** | `PascalCase` (не `UPPER_CASE` в C#) | `MaxRetryCount`, `DefaultRefreshInterval` |
| **Интерфейсы** | `I` + `PascalCase` | `ISessionService`, `ILocalizationService` |
| **Records** | `PascalCase` | `SessionInfo`, `AppWindowInfo` |
| **Enums** | `PascalCase` (значения тоже) | `SessionState.Active` |
| **Файлы** | `PascalCase.cs` | `MainWindow.xaml.cs`, `SettingsService.cs` |

### 3.2. Суффиксы и префиксы

| Суффикс/Префикс | Назначение | Пример |
|-----------------|------------|--------|
| `-ViewModel` | ViewModel классы | `MainViewModel`, `SessionViewModel` |
| `-Service` | Сервисы (DI) | `WtsSessionService`, `SettingsService` |
| `-Control` | UI контролы | `SessionItemControl`, `UserCellControl` |
| `-Dialog` | Диалоговые окна | `ElevateDialog`, `AboutDialog` |
| `Async` | Асинхронные методы | `EnumerateSessionsAsync`, `DisconnectAsync` |
| `Async` | Асинхронные события | `OnLoadedAsync`, `OnSessionSelectedAsync` |
| `On` + глагол | Обработчики событий | `OnConnectClicked`, `OnSettingsChanged` |
| `Is/Has/Can` | Boolean свойства | `IsElevated`, `HasSessions`, `CanDisconnect` |

### 3.3. Запреты

- ❌ Сокращения: `svc`, `mgr`, `cfg`, `info` → `service`, `manager`, `config`, `information`
- ❌ Венгерская нотация: `strName`, `intCount`
- ❌ Транслит: `Polzovatel`, `Sessiya`
- ❌ `data`, `obj`, `temp`, `helper` — бессмысленные имена

---

## 🎨 4. Стиль кода

### 4.1. Форматирование

| Правило | Значение |
|---------|----------|
| **Отступ** | 4 пробела |
| **Фигурные скобки** | На новой строке (K&R style для C#) |
| **Макс. длина строки** | 120 символов |
| **Region** | Не использовать |
| **`var`** | Использовать, когда тип очевиден из правой части |
| **`this.`** | Опускать, кроме конструкторов и конфликтов имён |

```csharp
// ✅ ПРАВИЛЬНО: K&R style, var где тип очевиден
public async Task<SessionInfo> GetSessionAsync(int sessionId, CancellationToken ct)
{
    var session = await _wtsService.GetSessionByIdAsync(sessionId, ct);
    return session ?? throw new InvalidOperationException($"Session {sessionId} not found");
}

// ✅ ПРАВИЛЬНО: явный тип, когда это улучшает читаемость
Dictionary<int, SessionViewModel> sessionMap = new();
```

### 4.2. Организация файла

```
1. Using (группировка: System → Microsoft → Project → Third-party)
2. Namespace
3. Класс (partial для XAML code-behind)
4.   ├── Константы
5.   ├── Приватные поля
6.   ├── Конструкторы
7.   ├── Публичные свойства/методы
8.   ├── Приватные методы
9.   └── Обработчики событий
```

### 4.3. Required и init-свойства (C# 11/12)

```csharp
// ✅ ПРАВИЛЬНО: record с required/init для immutable DTO
public sealed record SessionInfo
{
    public required int Id { get; init; }
    public required string UserName { get; init; }
    public string? FullName { get; init; }
    public WTSConnectState State { get; init; }
    public int ApplicationCount { get; init; }
    public bool IsLocked { get; init; }
    public bool IsScreensaverActive { get; init; }
}
```

---

## 🔒 5. Безопасность

| Правило | Описание |
|---------|----------|
| **Запрет `Process.Start(string)`** | Всегда `ProcessStartInfo` с валидацией `FileName` и `Arguments` |
| **Санитизация путей** | `Path.GetFullPath()`, проверка на `..`, `FileAttributes.ReparsePoint` |
| **Валидация ввода** | `Regex`, `maxLength`, whitelist для всех внешних данных |
| **Без PII в логах** | Не логировать пароли, имена пользователей, IP-адреса |
| **Минимальные привилегии** | По умолчанию без админ-прав, повышение через `runas` по требованию |
| **`unsafe` код** | Только при явной необходимости, с `[SuppressMessage]` и комментарием |

---

## 🧹 6. Управление ресурсами

| Паттерн | Применение |
|---------|------------|
| **`using var`** | Все `IDisposable`: `SafeHandle`, `CancellationTokenSource`, `StreamReader` |
| **`SafeHandle`** | Обёртка для всех P/Invoke хендлов (WTS, user32, kernel32) |
| **`CancellationToken`** | Все асинхронные методы, передача по цепочке |
| **`GCHandle`** | Для передачи managed объектов в native callbacks |
| **`ArrayPool<T>`** | Для временных массивов при EnumWindows, больших буферов |

```csharp
// ✅ ПРАВИЛЬНО: SafeHandle для WTS API
internal sealed class SafeWtsServerHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    private SafeWtsServerHandle() : base(true) { }

    protected override bool ReleaseHandle()
    {
        WTSCloseServer(handle);
        return true;
    }
}

// ✅ ПРАВИЛЬНО: using var для автоматической очистки
public async Task<IReadOnlyList<SessionInfo>> EnumerateAsync(CancellationToken ct)
{
    using var serverHandle = WtsNative.WTSOpenServer(Environment.MachineName);
    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
    cts.CancelAfter(TimeSpan.FromSeconds(10)); // Таймаут защиты от зависания

    return await Task.Run(() =>
    {
        // ... работа с WTS API
    }, cts.Token);
}
```

---

## ⚡ 7. Асинхронность и многопоточность

| Правило | Описание |
|---------|----------|
| **`async/await` для I/O** | Все вызовы к Win32 API — через `Task.Run` |
| **`ConfigureAwait(false)`** | В сервисах (не в UI-коде) |
| **`DispatcherQueue`** | Для обновления UI из фоновых потоков |
| **`lock`** | Для защиты общего состояния |
| **`ReaderWriterLockSlim`** | При частом чтении / редкой записи |
| **`Channel<T>`** | Для Producer-Consumer сценариев (очередь обновления сессий) |
| **`ValueTask`** | Для синхронных путей в async-методах |

```csharp
// ✅ ПРАВИЛЬНО: Channel<T> для очереди обновлений
private readonly Channel<SessionUpdate> _updateChannel = Channel.CreateBounded<SessionUpdate>(100);

public async Task StartRefreshLoopAsync(CancellationToken ct)
{
    var producer = Task.Run(async () =>
    {
        while (!ct.IsCancellationRequested)
        {
            var sessions = await _wtsService.EnumerateAsync(ct);
            foreach (var s in sessions)
                await _updateChannel.Writer.WriteAsync(new SessionUpdate(s), ct);
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
    }, ct);

    var consumer = ProcessUpdatesAsync(_updateChannel.Reader, ct);
    await Task.WhenAll(producer, consumer);
}
```

---

## 🧪 8. Тестирование

| Правило | Описание |
|---------|----------|
| **Фреймворк** | xUnit или MSTest |
| **Mock** | Moq для интерфейсов |
| **Именование тестов** | `Method_Scenario_ExpectedResult` |
| **Arrange-Act-Assert** | Обязательная структура |
| **Покрытие** | ≥ 80% для Services и ViewModels |

```csharp
// ✅ ПРАВИЛЬНО: именование и структура
[Fact]
public async Task DisconnectSessionAsync_AlreadyDisconnected_ReturnsTrue()
{
    // Arrange
    var mockService = new Mock<IWtsSessionService>();
    mockService.Setup(s => s.GetStateAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(WTSConnectState.Disconnected);
    var executor = new CommandExecutor(mockService.Object);

    // Act
    var result = await executor.DisconnectSessionAsync(1, CancellationToken.None);

    // Assert
    Assert.True(result);
    mockService.Verify(s => s.DisconnectAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
}
```

---

## 📐 9. XAML / WinUI стили

| Правило | Описание |
|---------|----------|
| **`x:Uid`** | Для всех локализуемых строк |
| **`x:Name`** | Только для элементов, используемых в code-behind |
| **Binding** | `x:Bind` (compiled binding) предпочтительно |
| **Styles** | Выносить в `ResourceDictionary`, не дублировать |
| **Converters** | Отдельные классы, не логика в XAML |
| **Mica** | `SystemBackdropConfiguration` для автоматической адаптации |

```xml
<!-- ✅ ПРАВИЛЬНО: x:Uid для локализации, x:Bind для binding -->
<TextBlock x:Uid="Toolbar_UsersTitle"
           Text="{x:Bind ViewModel.UsersTitle, Mode=OneWay}"
           Style="{StaticResource TitleTextBlockStyle}" />
```

---

## 🔀 10. Git-воркфлоу

### 10.1. Ветки

| Тип | Формат | Пример |
|-----|--------|--------|
| Основная | `main` | Защищена, требует PR |
| Фичи | `feature/описание` | `feature/column-drag-drop` |
| Исправления | `fix/описание` | `fix/session-state-aggregation` |
| Рефакторинг | `refactor/описание` | `refactor/wts-service-di` |

### 10.2. Коммиты

- **Язык:** русский
- **Формат:** `тип: описание (до 72 символов)`
- **Типы:** `fix:`, `chore:`, `feat:`, `refactor:`, `docs:`, `test:`, `perf:`

```
✅ feat: добавление drag-and-drop для столбцов таблицы
✅ fix: корректное определение заблокированной сессии
❌ "update", "fix bugs", "wip" — запрещены
```

### 10.3. Запреты

- ❌ Прямая работа с `main`
- ❌ Force-push в `main`
- ❌ Мерж без пройденных тестов

---

## 🚫 11. Антипаттерны (запрещено)

| Антипаттерн | Почему | Альтернатива |
|-------------|--------|-------------|
| `async void` (кроме event handlers) | Невозможно отловить исключения | `async Task` |
| `.Result` / `.Wait()` | Deadlock в UI-потоке | `await` |
| `dynamic` в критичном коде | Потеря типобезопасности | Strongly typed |
| `Reflection.Emit` | Сложность, безопасность | Source generators |
| Хардкод строк в XAML/C# | Невозможность локализации | `.resw` ресурсы |
| `Dispatcher.RunAsync` без await | Потеря контекста | `DispatcherQueue.TryEnqueue` |
| Глобальные `static` мутабельные поля | Race conditions | DI singleton, `ReaderWriterLockSlim` |
