using SmithForge.Features.InfoSystem;
using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace SmithForge.Main.Services
{
    public class MessageProcessor
    {
        private readonly AppSettings _settings;
        private readonly StickerPageService _stickerPageService;
        private string? _currentSessionId;
        private readonly Dictionary<string, IChatCommand> _commandMap;
        private static readonly Regex CommandRegex = new Regex(@"!!([^\s]+)", RegexOptions.Compiled);

        // ДОБАВЛЯЕМ: словарь сокращений из настроек
        private readonly Dictionary<string, string> _shortcuts;

        public MessageProcessor(AppSettings settings, InfoService infoService, StickerPageService stickerPageService, SoundPageService soundPageService)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _stickerPageService = stickerPageService;
            _commandMap = new Dictionary<string, IChatCommand>(StringComparer.OrdinalIgnoreCase);
            // ИСПРАВЛЕНО: загружаем сокращения из настроек
            if (settings.CommandShortcuts != null && settings.CommandShortcuts.Any())
            {
                _shortcuts = settings.GetCommandShortcutsAsDictionary();
                Debug.WriteLine($"[MessageProcessor] Загружено {_shortcuts.Count} сокращений команд");

                // Выводим все сокращения для проверки
                foreach (var shortcut in _shortcuts)
                {
                    Debug.WriteLine($"[MessageProcessor]   '{shortcut.Key}' -> '{shortcut.Value}'");
                }
            }
            else
            {
                _shortcuts = new Dictionary<string, string>();
                Debug.WriteLine("[MessageProcessor] Нет сокращений команд в настройках");

                // Для отладки: выводим содержимое settings.CommandShortcuts
                if (settings.CommandShortcuts == null)
                {
                    Debug.WriteLine("[MessageProcessor] settings.CommandShortcuts == null");
                }
                else if (!settings.CommandShortcuts.Any())
                {
                    Debug.WriteLine($"[MessageProcessor] settings.CommandShortcuts пуст, Count = {settings.CommandShortcuts.Count}");
                }
            }

            var commandsList = new List<IChatCommand>
            {
                new SoundCommand(soundPageService),
                new InfoCommand(infoService, stickerPageService),
                new BoldCommand(),
                new ItalicCommand(),
                new ColorCommand(),
                new ExtendCommand(),
                new LikeCommand(),
                new DislikeCommand(),
                new NickCommand(),
                new VoiceCommand(),
                new StickerCommand(),
                new AvatarCommand(),
                new HiddenCommand(),
                new VideoCommand(),
            };

            foreach (var cmd in commandsList)
            {
                _commandMap[cmd.Name] = cmd;
                if (cmd.Aliases != null)
                {
                    foreach (var alias in cmd.Aliases)
                    {
                        _commandMap[alias] = cmd;
                    }
                }
            }

            Debug.WriteLine($"[MessageProcessor] Всего команд в _commandMap: {_commandMap.Count}");
            Debug.WriteLine($"[MessageProcessor] Ключи: {string.Join(", ", _commandMap.Keys)}");
        }

        public event Action<Chater, CommonMessage, List<ChatCommandInfo>>? OnProcessed;
        public void SetSession(string sessionId) => _currentSessionId = sessionId;
        public string? GetSessionId() => _currentSessionId;

        // ДОБАВЛЯЕМ: метод замены сокращений
        private string ReplaceShortcuts(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
                return message;
            // ✅ ШАГ 1e: Паттерн iiN / ииN → !!info:NN_xxx (открыть страницу по номеру)
            // ii1 → !!info:01_profile
            // ии1 → !!info:01_profile
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[іiи]{2}(\d+)\b",
                match =>
                {
                    string numStr = match.Groups[1].Value;

                    if (!int.TryParse(numStr, out int pageNumber) || pageNumber <= 0)
                    {
                        Debug.WriteLine($"[Shortcuts] iiN: неверный номер '{numStr}'");
                        return match.Value;
                    }

                    string prefix = pageNumber.ToString("D2");  // 1 → "01"

                    var pagesDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Html", "InfoPages");

                    if (!Directory.Exists(pagesDir))
                    {
                        Debug.WriteLine($"[Shortcuts] iiN: папка не найдена: {pagesDir}");
                        return match.Value;
                    }

                    var candidates = Directory.GetFiles(pagesDir, $"{prefix}_*.html")
                        .OrderBy(f => f)
                        .ToArray();

                    if (candidates.Length > 0)
                    {
                        string fileName = Path.GetFileNameWithoutExtension(candidates[0]);
                        string replacement = $"!!info:{fileName}";
                        Debug.WriteLine($"[Shortcuts] ПАТТЕРН (iiN/ииN)! '{match.Value}' -> '{replacement}'");
                        return replacement;
                    }

                    Debug.WriteLine($"[Shortcuts] iiN: страница с номером {pageNumber} (префикс {prefix}_) не найдена");
                    return match.Value;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ШАГ 1c: Паттерн ссс → !!info:stickers (общая страница паков)
            // ссс → !!info:stickers
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[сc][сc][сc]\b",
                match =>
                {
                    string replacement = "!!info:stickers";
                    Debug.WriteLine($"[Shortcuts] ПАТТЕРН (справка общая)! '{match.Value}' -> '{replacement}'");
                    return replacement;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ШАГ 1d: Паттерн ссN → !!info:stickers:N (конкретный пак)
            // сс86 → !!info:stickers:86
            // сс98 → !!info:stickers (если пак №98 не существует, открываем общий список)
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[сc][сc](\d+)\b",
                match =>
                {
                    string packNumberStr = match.Groups[1].Value;

                    if (!int.TryParse(packNumberStr, out int packNumber))
                    {
                        Debug.WriteLine($"[Shortcuts] Не удалось распарсить номер: '{packNumberStr}'");
                        return match.Value;
                    }

                    // ✅ Проверяем, существует ли пак с таким номером
                    string packId = _stickerPageService?.GetPackIdByNumber(packNumber);

                    if (!string.IsNullOrEmpty(packId))
                    {
                        string replacement = $"!!info:stickers:{packNumber}";
                        Debug.WriteLine($"[Shortcuts] ПАТТЕРН (пак #{packNumber})! '{match.Value}' -> '{replacement}'");
                        return replacement;
                    }
                    else
                    {
                        // Пак не найден — открываем общий список паков
                        string replacement = "!!info:stickers";
                        Debug.WriteLine($"[Shortcuts] ПАТТЕРН (пак #{packNumber} НЕ НАЙДЕН, общая)! '{match.Value}' -> '{replacement}'");
                        return replacement;
                    }
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ШАГ 1a: Паттерн сXсY → !!st:X:Y (конкретный стикер)
            // с2с2 → !!st:2:2
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[сc](\d+)[сc](\d+)\b",
                match =>
                {
                    string packNumber = match.Groups[1].Value;
                    string stickerNumber = match.Groups[2].Value;
                    string replacement = $"!!st:{packNumber}:{stickerNumber}";
                    Debug.WriteLine($"[Shortcuts] ПАТТЕРН! '{match.Value}' -> '{replacement}'");
                    return replacement;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);

            // ✅ ШАГ 1b: Паттерн сXс → !!st:X:random (рандомный стикер)
            // с2с → !!st:2:random
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[сc](\d+)[сc]\b",
                match =>
                {
                    string packNumber = match.Groups[1].Value;
                    string replacement = $"!!st:{packNumber}:random";
                    Debug.WriteLine($"[Shortcuts] ПАТТЕРН (рандом)! '{match.Value}' -> '{replacement}'");
                    return replacement;
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ШАГ 1c: Паттерн abN → !!info:about:{файл с префиксом NN_}
            // ab4 → about/04_*.html
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\bab(\d+)\b",
                match =>
                {
                    string numStr = match.Groups[1].Value;

                    // Формируем двузначный префикс: 4 → "04"
                    string prefix = numStr.PadLeft(2, '0');

                    string aboutDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Html", "InfoPages", "about");

                    if (Directory.Exists(aboutDir))
                    {
                        // Ищем файл, начинающийся на "04_"
                        var files = Directory.GetFiles(aboutDir, $"{prefix}_*.html");
                        if (files.Length > 0)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(files[0]);
                            string replacement = $"!!info:about:{fileName}";
                            Debug.WriteLine($"[Shortcuts] ПАТТЕРН! '{match.Value}' -> '{replacement}'");
                            return replacement;
                        }
                    }

                    Debug.WriteLine($"[Shortcuts] Страница ab{numStr} (префикс {prefix}_) не найдена");
                    return "!!info:about";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ПАТТЕРН (русский): проN → !!info:about:{файл с префиксом NN_}
            // про4 → about/04_*.html
            message = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\b[пp][рr][оo](\d+)\b",
                match =>
                {
                    string numStr = match.Groups[1].Value;

                    string prefix = numStr.PadLeft(2, '0');

                    string aboutDir = Path.Combine(
                        AppDomain.CurrentDomain.BaseDirectory,
                        "Html", "InfoPages", "about");

                    if (Directory.Exists(aboutDir))
                    {
                        var files = Directory.GetFiles(aboutDir, $"{prefix}_*.html");
                        if (files.Length > 0)
                        {
                            string fileName = Path.GetFileNameWithoutExtension(files[0]);
                            string replacement = $"!!info:about:{fileName}";
                            Debug.WriteLine($"[Shortcuts] ПАТТЕРН (рус)! '{match.Value}' -> '{replacement}'");
                            return replacement;
                        }
                    }

                    Debug.WriteLine($"[Shortcuts] Страница про{numStr} (префикс {prefix}_) не найдена");
                    return "!!info:about";
                },
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            // ✅ ШАГ 2: Обычные сокращения из словаря
            if (_shortcuts.Count == 0)
                return message;

            var words = message.Split(' ');
            bool changed = false;

            for (int i = 0; i < words.Length; i++)
            {
                string word = words[i].ToLower();
                Debug.WriteLine($"[Shortcuts] Проверяем слово: '{word}'");

                if (_shortcuts.TryGetValue(word, out string? replacement))
                {
                    words[i] = replacement;
                    changed = true;
                    Debug.WriteLine($"[Shortcuts] ЗАМЕНА! '{word}' -> '{replacement}'");
                }
                else
                {
                    Debug.WriteLine($"[Shortcuts] Слово '{word}' не найдено в словаре");
                }
            }

            string result = changed ? string.Join(" ", words) : message;
            Debug.WriteLine($"[Shortcuts] Результат: '{result}'");
            return result;
        }
        public void Process(CommonMessage msg)
        {
            Debug.WriteLine($"[MessageProcessor] === НАЧАЛО ОБРАБОТКИ ===");
            Debug.WriteLine($"[MessageProcessor] Сообщение: '{msg.Message}'");
            Debug.WriteLine($"[MessageProcessor] Текущая сессия: '{_currentSessionId ?? "NULL"}'");

            if (msg == null) return;

            try
            {
                // ДОБАВЛЯЕМ: СНАЧАЛА заменяем сокращения из настроек
                msg.Message = ReplaceShortcuts(msg.Message);
                Debug.WriteLine($"[MessageProcessor] После замены сокращений: '{msg.Message}'");

                // Если пользователь уже установлен (из ProcessConnectorMessage), не ищем заново
                // Иначе ищем/создаём по Login
                if (msg.User == null)
                {
                    var tempChater = ChaterStorage.UpdateFromMessage(msg, _settings);
                    msg.User = tempChater;
                    Debug.WriteLine($"[MessageProcessor] Пользователь создан/загружен: {msg.User.Login}");
                }
                else
                {
                    Debug.WriteLine($"[MessageProcessor] Пользователь уже установлен: {msg.User.Login}");
                }

                // Теперь используем msg.User — он уже установлен
                var chater = msg.User;

                var commandsFound = ParseCommands(msg.Message);
                Debug.WriteLine($"[MessageProcessor] Найдено команд: {commandsFound.Count}");

                if (commandsFound.Count > 0)
                {
                    ProcessCommands(chater, msg, commandsFound);
                }
                else
                {
                    KarmaService.AddExperience(chater, msg, _settings);
                }

                // ✅ РАЗДЕЛЕНИЕ: техническое или визуальное?
                // Техническое = среди выполненных команд есть хотя бы одна с IsTechnical = true
                bool isTechnical = commandsFound.Any(c =>
                    _commandMap.TryGetValue(c.Name, out var cmd) &&
                    cmd is BaseCommand baseCmd &&
                    baseCmd.IsTechnical);

                bool isDashboardVisible = !commandsFound.Any(c =>
                    _commandMap.TryGetValue(c.Name, out var cmd) &&
                    cmd is BaseCommand baseCmd &&
                    !baseCmd.IsDashboardVisible);

                msg.IsVisible = !isTechnical && isDashboardVisible;

                Debug.WriteLine($"[MessageProcessor] Проверка сессии: _currentSessionId = '{_currentSessionId ?? "NULL"}'");
                Debug.WriteLine($"[MessageProcessor] Команды: [{string.Join(", ", commandsFound.Select(c => c.Name))}]");
                Debug.WriteLine($"[MessageProcessor] Тип: {(isTechnical ? "ТЕХНИЧЕСКОЕ" : "ВИЗУАЛЬНОЕ")}");

                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    var logMessage = new ChatLogMessage
                    {
                        SessionId = _currentSessionId,
                        ChaterId = chater.Id,
                        Message = msg.Message,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    };

                    if (isTechnical)
                    {
                        DatabaseService.SaveTechnicalMessage(logMessage);
                        msg.MessageNumber = 0;
                        Debug.WriteLine($"[Message] 🔧 ТЕХНИЧЕСКОЕ без номера: {msg.Message}");
                    }
                    else
                    {
                        DatabaseService.SaveChatMessage(logMessage);
                        msg.MessageNumber = logMessage.MessageNumber;
                        Debug.WriteLine($"[Message] ✅ ВИЗУАЛЬНОЕ #{msg.MessageNumber} от {chater.EffectiveName}");
                    }
                }
                else
                {
                    Debug.WriteLine($"[MessageProcessor] ⚠️ СЕССИЯ НЕ УСТАНОВЛЕНА! Сообщение НЕ СОХРАНЕНО!");
                    msg.MessageNumber = 0;
                }

                if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.Length >= _settings.MinMessageLength)
                {
                    // Если сообщение не обработано командой, проверяем можно ли оставить разметку
                    if (!msg.IsProcessedByCommand)
                    {
                        // Разрешаем прямую разметку с 3 ранга
                        if (chater.Rank >= 3)
                        {
                            // Оставляем теги - пользователь может писать разметку вручную
                            Debug.WriteLine($"[MARKUP] Прямая разметка разрешена для ранга {chater.Rank}");
                        }
                        else
                        {
                            // Удаляем все теги для низких рангов
                            string originalMessage = msg.Message;
                            msg.Message = RemoveAllTags(msg.Message);
                            if (originalMessage != msg.Message)
                            {
                                Debug.WriteLine($"[MARKUP] Теги удалены для ранга {chater.Rank}");
                            }
                        }
                    }
                    // ✅ Если сообщение обработано командой
                    else
                    {
                        // ✅ ОТОБРАЖАЕМЫЕ теги (показываются в чате) - ВКЛЮЧАЯ voice!
                        bool hasDisplayableTags = msg.Message.Contains("<b>") || msg.Message.Contains("</b>") ||
                                                  msg.Message.Contains("<i>") || msg.Message.Contains("</i>") ||
                                                  msg.Message.Contains("<color=") || msg.Message.Contains("</color>") ||
                                                  msg.Message.Contains("<c=") || msg.Message.Contains("</c>") ||
                                                  msg.Message.Contains("<voice>") || msg.Message.Contains("</voice>");

                        // ✅ СЛУЖЕБНЫЕ теги (НЕ показываются в чате, только действия)
                        bool hasServiceTags = msg.Message.Contains("<sticker") ||
                                              msg.Message.Contains("<like") ||
                                              msg.Message.Contains("<dislike") ||
                                              msg.Message.Contains("<nick") ||
                                              msg.Message.Contains("<sound") ||
                                              msg.Message.Contains("<hide>") || msg.Message.Contains("</hide>");

                        // ✅ Если есть ОТОБРАЖАЕМЫЕ теги - показываем в чате
                        if (hasDisplayableTags)
                        {
                            Debug.WriteLine($"[MessageProcessor] ✅ Есть отображаемые теги, показываем в чате");
                            // Продолжаем выполнение
                        }
                        // ✅ Если есть только СЛУЖЕБНЫЕ теги - НЕ показываем в чате, но обрабатываем
                        else if (hasServiceTags)
                        {
                            Debug.WriteLine($"[MessageProcessor] ⏭ Только служебные теги, скрываем из чата");
                            // Продолжаем выполнение (OnProcessed будет вызван для обработки)
                        }
                        // ❌ Если нет НИКАКИХ тегов - пропускаем полностью
                        else
                        {
                            Debug.WriteLine($"[MessageProcessor] ⏭ Команда выполнена, но нет тегов для отображения");
                            return;
                        }
                    }

                    // ✅ ВСЕГДА вызываем OnProcessed (для озвучивания, стикеров и т.д.)
                    msg.User = chater;
                    OnProcessed?.Invoke(chater, msg, commandsFound);
                }
                else
                {
                    Debug.WriteLine($"[UI-SKIP] Сообщение от {chater.Login} слишком короткое для отображения.");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MessageProcessor] Ошибка обработки сообщения: {ex.Message}");
            }

            Debug.WriteLine($"[MessageProcessor] === КОНЕЦ ОБРАБОТКИ ===");
        }

        private void ProcessCommands(Chater chater, CommonMessage msg, List<ChatCommandInfo> commandsFound)
        {
            Debug.WriteLine($"[CMD] Исходное сообщение: {msg.Message}");

            Debug.WriteLine($"[CMD] Всего команд в _commandMap: {_commandMap.Count}");
            Debug.WriteLine($"[CMD] Ключи в _commandMap: {string.Join(", ", _commandMap.Keys)}");

            Debug.WriteLine($"[CMD] Найдено команд в сообщении: {commandsFound.Count}");
            foreach (var c in commandsFound)
            {
                Debug.WriteLine($"[CMD]   - {c.Name} (Raw: {c.Raw})");
                Debug.WriteLine($"[CMD]     Есть в _commandMap? {_commandMap.ContainsKey(c.Name)}");
                if (_commandMap.ContainsKey(c.Name))
                {
                    var cmd = _commandMap[c.Name] as BaseCommand;
                    Debug.WriteLine($"[CMD]     MinRank: {cmd?.MinRank}, Cost: {cmd?.Cost}");
                    Debug.WriteLine($"[CMD]     CanExecute: {cmd?.CanExecute(chater)}");
                }
            }

            var availableCommands = commandsFound
                .Where(c => _commandMap.ContainsKey(c.Name))
                .Select(c => new {
                    Info = c,
                    Command = _commandMap[c.Name] as BaseCommand
                })
                .Where(c => c.Command != null && c.Command.CanExecute(chater))
                .OrderBy(c => c.Command.Cost)
                .ToList();

            Debug.WriteLine($"[CMD] Доступные команды после фильтрации: {availableCommands.Count}");
            foreach (var cmdInfo in availableCommands)
            {
                Debug.WriteLine($"[CMD]   - {cmdInfo.Info.Name} (Cost: {cmdInfo.Command.Cost})");
            }

            foreach (var cmd in commandsFound.Where(cmdInfo => _commandMap.ContainsKey(cmdInfo.Name) && !_commandMap[cmdInfo.Name].CanExecute(chater)))
            {
                var baseCmd = _commandMap[cmd.Name] as BaseCommand;
                Debug.WriteLine($"[CMD] {cmd.Name} недоступна (нужен ранг {baseCmd?.MinRank ?? 0}, у вас {chater.Rank})");
            }

            string cleanMessage = msg.Message;
            Debug.WriteLine($"[CMD] До удаления команд: {cleanMessage}");

            // ✅ Удаляем все команды из текста
            foreach (var cmd in commandsFound.OrderByDescending(c => c.Index))
            {
                Debug.WriteLine($"[CMD] Удаляем команду: {cmd.Raw} с позиции {cmd.Index}, длина {cmd.Length}");
                cleanMessage = cleanMessage.Remove(cmd.Index, cmd.Length).Trim();
                Debug.WriteLine($"[CMD] После удаления: {cleanMessage}");
            }

            double totalCost = 0;
            var executedCommands = new List<ChatCommandInfo>();
            bool anyCommandExecuted = false;

            foreach (var cmdInfo in availableCommands)
            {
                int commandCost = cmdInfo.Command.GetTotalCost(cmdInfo.Info, chater);

                if (chater.Karma >= totalCost + commandCost)
                {
                    Debug.WriteLine($"[CMD] Выполняем команду: {cmdInfo.Command.Name}");
                    Debug.WriteLine($"[CMD] Текст ДО выполнения: {cleanMessage}");

                    var tempMsg = new CommonMessage
                    {
                        Message = cleanMessage,  // ← передаем ТЕКУЩИЙ cleanMessage (может быть уже с тегами от предыдущей команды)
                        Type = msg.Type,
                        Login = msg.Login,
                        IsProcessedByCommand = false,
                        ShouldChargeForCommand = true
                    };

                    cmdInfo.Command.Execute(cmdInfo.Info, chater, tempMsg, _settings);

                    Debug.WriteLine($"[CMD] Текст ПОСЛЕ выполнения {cmdInfo.Command.Name}: {tempMsg.Message}");
                    Debug.WriteLine($"[CMD] IsProcessedByCommand: {tempMsg.IsProcessedByCommand}");
                    Debug.WriteLine($"[CMD] ShouldChargeForCommand: {tempMsg.ShouldChargeForCommand}");

                    // ✅ Обновляем cleanMessage результатом выполнения команды
                    if (tempMsg.IsProcessedByCommand)
                    {
                        cleanMessage = tempMsg.Message;
                        msg.DisplayTimeMs = tempMsg.DisplayTimeMs;
                        anyCommandExecuted = true;
                        Debug.WriteLine($"[CMD] Текст сохранен: {cleanMessage}");
                        Debug.WriteLine($"[CMD] Время сохранено: {msg.DisplayTimeMs}мс");
                    }

                    // Определяем, нужно ли списывать карму
                    bool shouldCharge = true;

                    // Проверка через ShouldChargeForCommand (установленный в команде или обработчике)
                    if (!tempMsg.ShouldChargeForCommand)
                    {
                        shouldCharge = false;
                        Debug.WriteLine($"[CMD] ShouldChargeForCommand = false - не списываем");
                    }

                    // Проверка через ShouldCharge (метод команды)
                    if (!cmdInfo.Command.ShouldCharge(cmdInfo.Info, chater, tempMsg))
                    {
                        shouldCharge = false;
                        Debug.WriteLine($"[CMD] ShouldCharge = false - не списываем");
                    }

                    if (shouldCharge)
                    {
                        totalCost += commandCost;
                        executedCommands.Add(cmdInfo.Info);
                        Debug.WriteLine($"[CMD] Выполнена {cmdInfo.Command.Name} (стоимость {commandCost})");
                    }
                    else
                    {
                        Debug.WriteLine($"[CMD] Команда {cmdInfo.Command.Name} выполнена бесплатно");
                    }
                }
                else
                {
                    Debug.WriteLine($"[CMD] Не хватает кармы на {cmdInfo.Command.Name} (нужно {commandCost})");
                }
            }

            if (totalCost > 0)
            {
                chater.Karma -= totalCost;
                chater.TotalKarma += totalCost;
                DatabaseService.UpdateChaterStats(chater);
                Debug.WriteLine($"[CMD] Списано {totalCost} кармы. Остаток: {chater.Karma:F1}");
            }

            // ✅ Универсальные маркеры технических команд для аналитики
            if (string.IsNullOrEmpty(cleanMessage) && executedCommands.Count > 0)
            {
                msg.Message = string.Join("",
                    executedCommands.Select(c => $"<cmd:{c.Name}/>").Distinct());
            }
            else
            {
                msg.Message = cleanMessage;
            }

            msg.IsProcessedByCommand = anyCommandExecuted || commandsFound.Count > 0;

            Debug.WriteLine($"[CMD] Финальный текст: {cleanMessage}");
            Debug.WriteLine($"[CMD] IsProcessedByCommand: {msg.IsProcessedByCommand}");
        }

        private List<ChatCommandInfo> ParseCommands(string text)
        {
            var results = new List<ChatCommandInfo>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            Debug.WriteLine($"[PARSE] Исходный текст: '{text}'");

            var matches = CommandRegex.Matches(text);
            Debug.WriteLine($"[PARSE] Найдено совпадений: {matches.Count}");

            foreach (Match m in matches)
            {
                Debug.WriteLine($"[PARSE] Найдена команда: '{m.Value}' на позиции {m.Index}");

                var fullPath = m.Groups[1].Value;
                var parts = fullPath.Split(':', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    Debug.WriteLine($"[PARSE] Части: {string.Join(" | ", parts)}");

                    results.Add(new ChatCommandInfo
                    {
                        Name = parts[0].ToLower(),
                        Arguments = parts.Skip(1).ToList(),
                        Raw = m.Value,
                        Index = m.Index,
                        Length = m.Length
                    });
                }
            }

            return results.OrderBy(cmd => cmd.Index).ToList();
        }

        private string RemoveAllTags(string input)
        {
            return Regex.Replace(input, @"<[^>]*>", string.Empty);
        }

    }
}