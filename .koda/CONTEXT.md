# SmithForge - Контекст рефакторинга MainViewModel

## Текущее состояние
- **MainViewModel:** 631 строка (было 1339, -53%)
- **Цель:** Дальнейшее уменьшение и разделение ответственности

## Выполненные шаги рефакторинга

### Шаг 1: YouTubeManagerViewModel
- Вынесено: Управление YouTube интеграцией
- Файл: `SmithForge/Features/YouTubeManager/ViewModels/YouTubeManagerViewModel.cs`
- Добавлен `MessageReceived` event для моста чата

### Шаг 2: OverlayManagerService
- Вынесено: Управление 4 оверлеями (Main, Shorts, Important, Stickers)
- Файл: `SmithForge/Main/Services/OverlayManagerService.cs`
- Заменена разрозненная инициализация, переключение режимов, сохранение позиций

### Шаг 3: ChatConnectionService
- Вынесено: Подключение/отключение платформ (YouTube, Twitch, GoodGame)
- Файл: `SmithForge/Main/Services/ChatConnectionService.cs`
- Созданы коннекторы, подписка на события, логика отключения

### Шаг 4: StreamSessionManager
- Вынесено: Управление жизненным циклом сессий стримов
- Файл: `SmithForge/Main/Services/StreamSessionManager.cs`
- Создание, сохранение, переключение номеров сессий

### Шаг 5: MessageHandlerService
- Вынесено: Обработка сообщений, маршрутизация команд, обновления оверлеев
- Файл: `SmithForge/Main/Services/MessageHandlerService.cs`
- `MessageProcessor` изменён с `internal` на `public`

### Шаг 6: SettingsService
- Вынесено: Управление всеми настройками (YouTube, звук, режимы, оверлеи)
- Файл: `SmithForge/Main/Services/SettingsService.cs`
- Заменены все `partial void On*Changed` методы

### Шаг 7: DialogService
- Вынесено: Диалоговые окна (ввод Video ID)
- Файл: `SmithForge/Main/Services/DialogService.cs`
- Удалён ~150-строчный метод `ShowVideoIdDialogAsync`

## Созданные сервисы (полный список)
1. `OverlayManagerService` — управление 4 оверлеями
2. `ChatConnectionService` — подключение/отключение чатов
3. `StreamSessionManager` — управление сессиями стримов
4. `MessageHandlerService` — обработка сообщений
5. `SettingsService` — управление всеми настройками
6. `DialogService` — диалоговые окна

## Изменения в UI
- `ChatManagerWindow.xaml`: кнопки "Сохранить все" и "Добавить чат" перемещены в одну строку

## Потенциальные направления для дальнейшего рефакторинга
- `AddKarmaToAll` (~50 строк) → `KarmaService`
- `ToggleDashboard`, `ToggleShortsOverlay`, `ToggleImportantOverlay`, `ToggleStickersOverlay` (~30 строк) → `OverlayManagerService`
- `Launch`, `Start`, `Stop`, `StartPolling` (~60 строк) → `ExternalChatService`
- `OnYouTubeManagerMessageReceived`, `OnMessageProcessed` (~100 строк) → `MessageHandlerService`

## Известные нюансы
- `ImportantQueueService` — статический класс, нельзя передавать как параметр конструктора
- Инициализация сервисов в `MainViewModel()` должна происходить ДО установки свойств с `partial void` (YouTubeApiKey, и т.д.)
- `SettingsService` зависит от `OverlayManagerService`, но НЕ от `ImportantQueueService`