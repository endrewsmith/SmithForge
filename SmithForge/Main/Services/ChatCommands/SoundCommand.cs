using SmithForge.Features.InfoSystem;
using SmithForge.Main.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Main.Services.ChatCommands
{
    // Main/Services/ChatCommands/SoundCommand.cs

    public class SoundCommand : BaseCommand
    {
        private readonly SoundPageService _soundPageService;

        public override string Name => "snd";
        public override IEnumerable<string> Aliases => new[] { "звук", "sound" };
        public override string Description => "Воспроизвести звук: !!snd:pack:номер";
        public override int Cost => 2;
        public override int MinRank => 0;
        public override int[] FreeForRanks => new[] { 5 };

        public SoundCommand(SoundPageService soundPageService)
        {
            _soundPageService = soundPageService;
        }

        public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
        {
            if (info.Arguments.Count == 0)
            {
                msg.Message = "❌ Укажите пак и звук: !!snd:pack:номер";
                msg.IsProcessedByCommand = true;
                return;
            }

            string packInput = info.Arguments[0];
            string soundId = info.Arguments.Count > 1 ? info.Arguments[1] : "1";

            // Находим пак
            var packId = _soundPageService.ResolvePackId(packInput);
            if (packId == null)
            {
                msg.Message = $"❌ Пак '{packInput}' не найден";
                msg.IsProcessedByCommand = true;
                return;
            }

            var pack = _soundPageService.GetPack(packId);
            if (pack == null)
            {
                msg.Message = $"❌ Пак не найден";
                msg.IsProcessedByCommand = true;
                return;
            }

            // Находим звук
            var sound = pack.Sounds.FirstOrDefault(s => s.Id == soundId);
            if (sound == null)
            {
                var paddedId = soundId.PadLeft(3, '0');
                sound = pack.Sounds.FirstOrDefault(s => s.Id == paddedId);
            }

            if (sound == null)
            {
                msg.Message = $"❌ Звук #{soundId} не найден";
                msg.IsProcessedByCommand = true;
                return;
            }

            // Отправляем звук
            var soundPath = sound.FilePath.Replace(
                AppDomain.CurrentDomain.BaseDirectory,
                "/").Replace("\\", "/");

            msg.Message = $"<sound pack='{packId}' id='{soundId}' path='{soundPath}' />";
            msg.IsProcessedByCommand = true;
        }
    }
}
