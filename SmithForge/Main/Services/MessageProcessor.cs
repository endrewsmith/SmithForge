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
        private string? _currentSessionId;  // <-- ЭТО ПОЛЕ БЫЛО ПРОПУЩЕНО

        // Мапа для быстрого поиска команд по имени или алиасу
        private readonly Dictionary<string, IChatCommand> _commandMap;

        // Регулярка ищет !! и всё до первого пробела (напр. !!mute:ivan:10)
        private static readonly Regex CommandRegex = new Regex(@"!!([^\s]+)", RegexOptions.Compiled);

        public MessageProcessor(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _commandMap = new Dictionary<string, IChatCommand>(StringComparer.OrdinalIgnoreCase);

            // --- 1. СПИСОК ВСЕХ ЭКЗЕМПЛЯРОВ КОМАНД ---
            var commandsList = new List<IChatCommand>
            {
                new HelpCommand(_commandMap), 
                // Добавляй сюда новые классы команд по мере создания
            };

            // --- 2. РЕГИСТРАЦИЯ (Основное имя + Алиасы) ---
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
        }

        // Событие для MainViewModel
        public event Action<Chater, CommonMessage, List<ChatCommand>>? OnProcessed;

        public void SetSession(string sessionId) => _currentSessionId = sessionId;

        public void Process(CommonMessage msg)
        {
            if (msg == null) return;

            try
            {
                // 1. Обновляем юзера (БД + Кэш)
                var chater = ChaterStorage.UpdateFromMessage(msg, _settings);
                msg.User = chater;

                // 2. Парсим команды (!!cmd:arg1)
                var commandsFound = ParseCommands(msg.Message);

                // 3. Выполняем логику команд
                if (commandsFound.Count > 0)
                {
                    foreach (var cmd in commandsFound)
                    {
                        if (_commandMap.TryGetValue(cmd.Name, out var commandAction))
                        {
                            commandAction.Execute(cmd, chater, msg, _settings);
                        }
                        else
                        {
                            Debug.WriteLine($"[CMD] Неизвестная команда: {cmd.Name}");
                        }
                    }
                }
                else
                {
                    // 4. Начисление опыта за общение (если не было команд)
                    KarmaService.AddExperience(chater, msg, _settings);
                }

                // 5. Логируем сообщение в историю стрима
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

                // 6. ЖЕСТКИЙ ФИЛЬТР UI
                if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.Length >= _settings.MinMessageLength)
                {
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

        private List<ChatCommand> ParseCommands(string text)
        {
            var results = new List<ChatCommand>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            var matches = CommandRegex.Matches(text);
            foreach (Match m in matches)
            {
                var fullPath = m.Groups[1].Value;
                var parts = fullPath.Split(':', StringSplitOptions.RemoveEmptyEntries);

                if (parts.Length > 0)
                {
                    results.Add(new ChatCommand
                    {
                        Name = parts[0].ToLower(),
                        Arguments = parts.Skip(1).ToList(),
                        Raw = m.Value
                    });
                }
            }
            return results;
        }
    }
}