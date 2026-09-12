using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services.ChatCommands
{
    class VoiceCommand : BaseCommand
    {
        // ✅ ОСНОВНОЕ ИМЯ КОМАНДЫ
        public override string Name => "voice";

        // ✅ АЛИАСЫ (русские и английские варианты)
        public override IEnumerable<string> Aliases => new[] {
            "голос",        // русский
            "озвучить",     // русский
            "say",          // английский
            "speak",        // английский
            "войс",
        };

        public override string Description => "Озвучить сообщение: !!voice текст или !!voice:ж текст (женский голос)";
        public override int Cost => 5;
        public override int MinRank => 1;
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            string voiceParam = info.Arguments.Count > 0 ? info.Arguments[0].ToLower() : "";
            string messageText = msg.Message;

            Debug.WriteLine($"[VoiceCommand] ==========================================");
            Debug.WriteLine($"[VoiceCommand] Параметр голоса: '{voiceParam}'");
            Debug.WriteLine($"[VoiceCommand] Текст: '{messageText}'");

            // Получаем список голосов
            var voices = VoiceService.GetAvailableVoiceNames();
            Debug.WriteLine($"[VoiceCommand] Доступно голосов: {voices.Count}");

            string selectedVoice = null;

            if (voices.Count > 0)
            {
                switch (voiceParam)
                {
                    case "м":
                    case "male":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Dmitry", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Pavel", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Male", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран мужской голос: {selectedVoice}");
                        break;

                    case "ж":
                    case "female":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Irina", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Svetlana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Tatyana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Female", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран женский голос: {selectedVoice}");
                        break;

                    case "0":
                    case "default":
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        Debug.WriteLine($"[VoiceCommand] Выбран голос по умолчанию (0): {selectedVoice}");
                        break;

                    case "р":
                    case "random":
                        var random = new Random();
                        selectedVoice = voices[random.Next(voices.Count)];
                        Debug.WriteLine($"[VoiceCommand] Выбран случайный голос: {selectedVoice}");
                        break;

                    default:
                        if (int.TryParse(voiceParam, out int number) && number > 0)
                        {
                            int index = number - 1;
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
                            // Если параметр не распознан - пробуем найти голос по имени
                            var match = voices.FirstOrDefault(v =>
                                v.Contains(voiceParam, StringComparison.OrdinalIgnoreCase));
                            if (match != null)
                            {
                                selectedVoice = match;
                                Debug.WriteLine($"[VoiceCommand] Выбран голос по имени: {selectedVoice}");
                            }
                            else
                            {
                                selectedVoice = voices[0];
                                Debug.WriteLine($"[VoiceCommand] Неизвестный параметр '{voiceParam}', выбран первый голос: {selectedVoice}");
                            }
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

            // ✅ ИСПОЛЬЗУЕМ ТЕГ <voice> ДЛЯ ОЗВУЧИВАНИЯ
            msg.Message = $"<voice>{messageText}</voice>";
            msg.IsProcessedByCommand = true;
            msg.ShouldChargeForCommand = true;

            Debug.WriteLine($"[VoiceCommand] ==========================================");
        }

        public override bool ShouldCharge(ChatCommandInfo info, Chater chater, CommonMessage msg)
        {
            return msg.ShouldChargeForCommand;
        }
    }
}