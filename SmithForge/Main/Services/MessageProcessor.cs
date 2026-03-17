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
        private readonly Regex _commandRegex;
        public MessageProcessor(AppSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));

            // Создаем регулярку из всех префиксов
            string prefixes = string.Join("|", settings.CommandPrefixes.Select(p => Regex.Escape(p)));
            _commandRegex = new Regex($@"({prefixes})([^\s]+)", RegexOptions.Compiled);
            _commandMap = new Dictionary<string, IChatCommand>(StringComparer.OrdinalIgnoreCase);

            var commandsList = new List<IChatCommand>
            {
                new HelpCommand(_commandMap),
                new BoldCommand(),
                new ItalicCommand(),
                new ColorCommand(),
                new ExtendCommand(),
                new LikeCommand(),
                new DislikeCommand(),
                new NickCommand(),
                new ImportantCommand(),
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
        }

        public event Action<Chater, CommonMessage, List<ChatCommandInfo>>? OnProcessed;
        public void SetSession(string sessionId) => _currentSessionId = sessionId;

        public void Process(CommonMessage msg)
        {
            if (msg == null) return;

            try
            {
                var chater = ChaterStorage.UpdateFromMessage(msg, _settings);
                msg.User = chater;

                // 1. Ищем все уникальные команды в сообщении
                var commandsFound = ParseCommands(msg.Message);

                if (commandsFound.Count > 0)
                {
                    // 2. ОЧИСТКА: Удаляем ВСЕ вхождения команд из текста (и !!bold, и !!важно)
                    // Чтобы не было "!!bold !!bold текст" -> "<b><b>текст</b></b>"
                    msg.Message = CleanMessageFromCommands(msg.Message, commandsFound);

                    // 3. Применяем логику команд к уже ЧИСТОМУ тексту
                    ProcessCommands(chater, msg, commandsFound);
                }
                else
                {
                    KarmaService.AddExperience(chater, msg, _settings);
                }

                // ... далее твой код логирования в БД без изменений ...
                if (!string.IsNullOrEmpty(_currentSessionId))
                {
                    var logMessage = new ChatLogMessage
                    {
                        SessionId = _currentSessionId,
                        ChaterId = chater.Id,
                        Message = msg.Message,
                        Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds()
                    };
                    DatabaseService.SaveChatMessage(logMessage);
                    msg.MessageNumber = logMessage.MessageNumber;
                }

                // Финальная проверка и отправка на UI
                if (!string.IsNullOrWhiteSpace(msg.Message) && msg.Message.Length >= _settings.MinMessageLength)
                {
                    // Если ранг маленький и это не команда - чистим ручные теги < >
                    if (!msg.IsProcessedByCommand && chater.Rank < 5)
                    {
                        msg.Message = RemoveAllTags(msg.Message);
                    }

                    OnProcessed?.Invoke(chater, msg, commandsFound);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MessageProcessor] Ошибка: {ex.Message}");
            }
        }

        private string CleanMessageFromCommands(string text, List<ChatCommandInfo> commands)
        {
            string result = text;

            // Сортируем по длине (сначала длинные), чтобы !!bold:red не превратился в :red после удаления !!bold
            var sortedCommands = commands.OrderByDescending(c => c.Raw.Length).ToList();

            foreach (var cmd in sortedCommands)
            {
                // Используем .Raw — именно так у тебя называется свойство с !!
                string pattern = System.Text.RegularExpressions.Regex.Escape(cmd.Raw);

                // Вырезаем все вхождения этой команды подчистую
                result = System.Text.RegularExpressions.Regex.Replace(
                    result,
                    pattern,
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }

            return result.Trim();
        }




        private void ProcessCommands(Chater chater, CommonMessage msg, List<ChatCommandInfo> commandsFound)
        {
            Debug.WriteLine($"[CMD] Исходное сообщение: {msg.Message}");

            // 1. ФИЛЬТРАЦИЯ ДОСТУПНЫХ КОМАНД
            // Группируем по имени, чтобы обработать каждый тип команды (bold, важно) только ОДИН раз
            var availableCommands = commandsFound
                .Where(c => _commandMap.ContainsKey(c.Name))
                .Select(c => new {
                    Info = c,
                    Command = _commandMap[c.Name] as BaseCommand
                })
                .Where(c => c.Command != null && c.Command.CanExecute(chater))
                .GroupBy(c => c.Info.Name.ToLower()) // Группировка по имени команды
                .Select(g => g.First()) // Берем только первое вхождение каждого типа
                .OrderBy(c => c.Command.Cost)
                .ToList();

            // 2. БЕЗОПАСНАЯ ОЧИСТКА ТЕКСТА ОТ ВСЕХ КОМАНД
            string cleanMessage = msg.Message;

            // Сортируем все найденные вхождения по длине (от длинных к коротким), 
            // чтобы сначала удалить !!important, а потом !!imp
            var allRawCommands = commandsFound.OrderByDescending(c => c.Raw.Length).ToList();

            foreach (var cmd in allRawCommands)
            {
                // Используем Regex для удаления всех вхождений текста команды без привязки к индексам
                string pattern = System.Text.RegularExpressions.Regex.Escape(cmd.Raw);
                cleanMessage = System.Text.RegularExpressions.Regex.Replace(
                    cleanMessage,
                    pattern,
                    "",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            cleanMessage = cleanMessage.Trim();
            Debug.WriteLine($"[CMD] Текст после полной очистки: {cleanMessage}");

            double totalCost = 0;
            bool anyCommandExecuted = false;

            // 3. ВЫПОЛНЕНИЕ УНИКАЛЬНЫХ КОМАНД
            foreach (var cmdItem in availableCommands)
            {
                int commandCost = cmdItem.Command.GetTotalCost(cmdItem.Info, chater);

                if (chater.Karma >= totalCost + commandCost)
                {
                    Debug.WriteLine($"[CMD] Выполняем уникальную команду: {cmdItem.Command.Name}");

                    var tempMsg = new CommonMessage
                    {
                        Message = cleanMessage,
                        Type = msg.Type,
                        Login = msg.Login,
                        IsProcessedByCommand = false,
                        ShouldChargeForCommand = true
                    };

                    // Выполняем логику команды (например, оборачиваем в <b>)
                    cmdItem.Command.Execute(cmdItem.Info, chater, tempMsg, _settings);

                    if (tempMsg.IsProcessedByCommand)
                    {
                        cleanMessage = tempMsg.Message;
                        msg.DisplayTimeMs = tempMsg.DisplayTimeMs;
                        anyCommandExecuted = true;

                        // Проверяем, нужно ли списывать карму
                        bool shouldCharge = tempMsg.ShouldChargeForCommand &&
                                           cmdItem.Command.ShouldCharge(cmdItem.Info, chater, tempMsg);

                        if (shouldCharge)
                        {
                            totalCost += commandCost;
                            Debug.WriteLine($"[CMD] Списана стоимость {commandCost} за {cmdItem.Command.Name}");
                        }
                    }
                }
                else
                {
                    Debug.WriteLine($"[CMD] Не хватает кармы на {cmdItem.Command.Name}");
                }
            }

            // 4. ФИНАЛИЗАЦИЯ
            if (totalCost > 0)
            {
                chater.Karma -= totalCost;
                chater.TotalKarma += totalCost;
                DatabaseService.UpdateChaterStats(chater);
            }

            msg.Message = cleanMessage;
            msg.IsProcessedByCommand = anyCommandExecuted;

            Debug.WriteLine($"[CMD] Финальный результат: {msg.Message}");
        }

        private List<ChatCommandInfo> ParseCommands(string text)
        {
            var results = new List<ChatCommandInfo>();
            if (string.IsNullOrWhiteSpace(text)) return results;

            Debug.WriteLine($"[PARSE] Исходный текст: '{text}'");

            var matches = _commandRegex.Matches(text);
            Debug.WriteLine($"[PARSE] Найдено совпадений: {matches.Count}");

            foreach (Match m in matches)
            {
                Debug.WriteLine($"[PARSE] Найдена команда: '{m.Value}' на позиции {m.Index}");

                // Группа 1 - префикс (!!, .., ,,)
                // Группа 2 - остальная часть команды
                string fullPath = m.Groups[2].Value;
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