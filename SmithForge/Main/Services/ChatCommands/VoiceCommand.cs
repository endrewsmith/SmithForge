using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    public class VoiceCommand : BaseCommand
    {
        public override string Name => "важно";
        public override IEnumerable<string> Aliases => new[] { "important", "важное", "imp" };

        public override string Description => "Пометить сообщение как важное (озвучивается)";
        public override int Cost => 5;
        public override int MinRank => 1;
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            string voiceParam = info.Arguments.Count > 0 ? info.Arguments[0].ToLower() : "";
            string messageText = msg.Message;

            // ========== ОТЛАДОЧНЫЙ ВЫВОД ==========
            Debug.WriteLine($"[VoiceCommand] ==========================================");
            Debug.WriteLine($"[VoiceCommand] Параметр голоса: '{voiceParam}'");
            Debug.WriteLine($"[VoiceCommand] Текст: '{messageText}'");
            Debug.WriteLine($"[VoiceCommand] info.Arguments.Count = {info.Arguments.Count}");
            for (int i = 0; i < info.Arguments.Count; i++)
            {
                Debug.WriteLine($"[VoiceCommand] Argument[{i}] = '{info.Arguments[i]}'");
            }

            // Получаем список голосов
            var voices = VoiceService.GetAvailableVoiceNames();
            Debug.WriteLine($"[VoiceCommand] Доступно голосов: {voices.Count}");
            for (int i = 0; i < voices.Count; i++)
            {
                Debug.WriteLine($"[VoiceCommand]   {i + 1}. {voices[i]}");
            }
            // =====================================

            string selectedVoice = null;

            if (voices.Count > 0)
            {
                switch (voiceParam)
                {
                    case "м":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Dmitry", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Pavel", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Male", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран мужской голос: {selectedVoice}");
                        break;

                    case "ж":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Irina", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Svetlana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Tatyana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Female", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран женский голос: {selectedVoice}");
                        break;

                    case "0":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран голос по умолчанию (0): {selectedVoice}");
                        break;

                    case "р":
                        var random = new Random();
                        selectedVoice = voices[random.Next(voices.Count)];
                        Debug.WriteLine($"[VoiceCommand] Выбран случайный голос: {selectedVoice}");
                        break;

                    default:
                        if (int.TryParse(voiceParam, out int number) && number > 0)
                        {
                            int index = number - 1;
                            Debug.WriteLine($"[VoiceCommand] Парсим как число: {number}, индекс: {index}");
                            if (index < voices.Count)
                            {
                                selectedVoice = voices[index];
                                Debug.WriteLine($"[VoiceCommand] Выбран голос по номеру {number}: {selectedVoice}");
                            }
                            else
                            {
                                selectedVoice = voices[0];
                                Debug.WriteLine($"[VoiceCommand] Номер {number} превышает количество голосов ({voices.Count}), выбран первый: {selectedVoice}");
                            }
                        }
                        else if (!string.IsNullOrEmpty(voiceParam))
                        {
                            selectedVoice = voices[0];
                            Debug.WriteLine($"[VoiceCommand] Неизвестный параметр '{voiceParam}', выбран первый голос: {selectedVoice}");
                        }
                        else
                        {
                            selectedVoice = voices.FirstOrDefault(v =>
                                v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                            Debug.WriteLine($"[VoiceCommand] Без параметра, выбран голос по умолчанию: {selectedVoice}");
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(selectedVoice))
                {
                    VoiceService.SelectVoice(selectedVoice);
                    settings.SelectedVoice = selectedVoice;
                    Debug.WriteLine($"[VoiceCommand] ✅ Голос установлен: {selectedVoice}");
                }
            }

            // Помечаем сообщение как важное
            msg.Message = $"<important>{messageText}</important>";
            msg.IsProcessedByCommand = true;

            Debug.WriteLine($"[VoiceCommand] ==========================================");
        }
    }
}