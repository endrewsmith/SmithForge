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
        public override int MinRank => 0;
        public override int[] FreeForRanks => new[] { 5 };

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            string voiceParam = info.Arguments.Count > 0 ? info.Arguments[0].ToLower() : "";
            string messageText = msg.Message;

            Debug.WriteLine($"[VoiceCommand] Параметр голоса: '{voiceParam}', Текст: '{messageText}'");

            // Выбираем голос в зависимости от параметра
            string selectedVoice = null;
            var voices = VoiceService.GetAvailableVoiceNames();

            if (voices.Count > 0)
            {
                switch (voiceParam)
                {
                    case "м":  // мужской голос
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Dmitry", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Pavel", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Male", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        break;

                    case "ж":  // женский голос
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Irina", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Svetlana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Tatyana", StringComparison.OrdinalIgnoreCase) ||
                            v.Contains("Female", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        break;

                    case "0":  // голос по умолчанию
                        selectedVoice = voices.FirstOrDefault(v =>
                            v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        break;

                    case "р":  // случайный голос
                        var random = new Random();
                        selectedVoice = voices[random.Next(voices.Count)];
                        break;

                    default:   // если цифра — выбираем по номеру
                        if (int.TryParse(voiceParam, out int number) && number > 0)
                        {
                            int index = number - 1;
                            if (index < voices.Count)
                            {
                                selectedVoice = voices[index];
                                Debug.WriteLine($"[VoiceCommand] Выбор по номеру {number}: {selectedVoice}");
                            }
                            else
                            {
                                selectedVoice = voices[0];
                                Debug.WriteLine($"[VoiceCommand] Номер {number} превышает количество голосов, выбран первый");
                            }
                        }
                        else
                        {
                            // Без параметра или неизвестный параметр — голос по умолчанию
                            selectedVoice = voices.FirstOrDefault(v =>
                                v.Contains("Aleksandr", StringComparison.OrdinalIgnoreCase)) ?? voices[0];
                        }
                        break;
                }

                if (!string.IsNullOrEmpty(selectedVoice))
                {
                    VoiceService.SelectVoice(selectedVoice);
                    settings.SelectedVoice = selectedVoice;
                    Debug.WriteLine($"[VoiceCommand] Выбран голос: {selectedVoice}");
                }
            }

            // Помечаем сообщение как важное
            msg.Message = $"<important>{messageText}</important>";
            msg.IsProcessedByCommand = true;
        }
    }
}