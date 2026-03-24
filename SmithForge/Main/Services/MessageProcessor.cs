using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    internal class MessageProcessor
    {
        private readonly AppSettings _settings;
        private string? _currentSessionId;
        private readonly Dictionary<string, IChatCommand> _commandMap;
        private static readonly Regex CommandRegex = new Regex(@"!!([^\s]+)", RegexOptions.Compiled);

        // ДОБАВЛЯЕМ: словарь сокращений из настроек
        private readonly Dictionary<string, string> _shortcuts;

        public MessageProcessor(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
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
                new HelpCommand(_commandMap),
                new BoldCommand(),
                new ItalicCommand(),
                new ColorCommand(),
                new ExtendCommand(),
                new LikeCommand(),
                new DislikeCommand(),
                new NickCommand(),      // ДОБАВЛЯЕМ
new VoiceCommand(),
                new StickerCommand(),   // ДОБАВЛЯЕМ
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

        // ДОБАВЛЯЕМ: метод замены сокращений
        private string ReplaceShortcuts(string message)
        {
            if (string.IsNullOrWhiteSpace(message) || _shortcuts.Count == 0)
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

            if (msg == null) return;

            try
            {
                // ДОБАВЛЯЕМ: СНАЧАЛА заменяем сокращения из настроек
                msg.Message = ReplaceShortcuts(msg.Message);
                Debug.WriteLine($"[MessageProcessor] После замены сокращений: '{msg.Message}'");

                var chater = ChaterStorage.UpdateFromMessage(msg, _settings);
                msg.User = chater;

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

                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    var logMessage = new ChatLogMessage
                    {
                        SessionId = _currentSessionId,
                        ChaterId = chater.Id,
                        Message = msg.Message,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        Likes = 0,
                        Dislikes = 0
                    };

                    DatabaseService.SaveChatMessage(logMessage);
                    msg.MessageNumber = logMessage.MessageNumber;

                    Debug.WriteLine($"[Message] Стрим #{_currentSessionId}, Сообщение #{msg.MessageNumber} от {chater.EffectiveName}");
                }

                if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.Length >= _settings.MinMessageLength)
                {
                    // Если сообщение не обработано командой, проверяем можно ли оставить разметку
                    if (!msg.IsProcessedByCommand)
                    {
                        // Разрешаем прямую разметку только с 5 ранга
                        if (chater.Rank >= 5)
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

            foreach (var cmd in commandsFound.OrderByDescending(c => c.Index))
            {
                Debug.WriteLine($"[CMD] Удаляем команду: {cmd.Raw} с позиции {cmd.Index}, длина {cmd.Length}");
                cleanMessage = cleanMessage.Remove(cmd.Index, cmd.Length).Trim();
                Debug.WriteLine($"[CMD] После удаления: {cleanMessage}");
            }

            double totalCost = 0;
            var executedCommands = new List<ChatCommandInfo>();

            foreach (var cmdInfo in availableCommands)
            {
                int commandCost = cmdInfo.Command.GetTotalCost(cmdInfo.Info, chater);

                if (chater.Karma >= totalCost + commandCost)
                {
                    Debug.WriteLine($"[CMD] Выполняем команду: {cmdInfo.Command.Name}");
                    Debug.WriteLine($"[CMD] Текст ДО выполнения: {cleanMessage}");

                    var tempMsg = new CommonMessage
                    {
                        Message = cleanMessage,
                        Type = msg.Type,
                        Login = msg.Login,
                        IsProcessedByCommand = false,
                        ShouldChargeForCommand = true // по умолчанию списываем
                    };

                    cmdInfo.Command.Execute(cmdInfo.Info, chater, tempMsg, _settings);

                    Debug.WriteLine($"[CMD] Текст ПОСЛЕ выполнения: {tempMsg.Message}");
                    Debug.WriteLine($"[CMD] IsProcessedByCommand: {tempMsg.IsProcessedByCommand}");
                    Debug.WriteLine($"[CMD] ShouldChargeForCommand: {tempMsg.ShouldChargeForCommand}");

                    if (tempMsg.IsProcessedByCommand)
                    {
                        cleanMessage = tempMsg.Message;
                        msg.DisplayTimeMs = tempMsg.DisplayTimeMs;
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

            msg.Message = cleanMessage;
            // Если в сообщении была хоть одна команда (даже неудачная) — помечаем как обработанное
            msg.IsProcessedByCommand = commandsFound.Count > 0;

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