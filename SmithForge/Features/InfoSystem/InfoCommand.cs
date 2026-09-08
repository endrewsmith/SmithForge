// Features/InfoSystem/InfoCommand.cs

using SmithForge.Features.InfoSystem;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using System.Diagnostics;

public class InfoCommand : BaseCommand
{
    private readonly InfoService _infoService;
    private readonly StickerPageService _stickerPageService;

    public override string Name => "info";
    public override IEnumerable<string> Aliases => new[] { "инфо", "i", "справка" };
    public override string Description => "Информационный справочник: !!info [раздел|..|search:текст]";
    public override int Cost => 0;
    public override int MinRank => 0;
    public override int[] FreeForRanks => new[] { 0, 1, 2, 3, 4, 5 };

    public InfoCommand(InfoService infoService, StickerPageService stickerPageService)
    {
        _infoService = infoService;
        _stickerPageService = stickerPageService;
    }

    public override void Execute(ChatCommandInfo info, Chater chater, CommonMessage msg, AppSettings settings)
    {
        string userId = chater.Id;

        // ✅ Склеиваем ВСЕ аргументы через "/" для создания пути любой глубины
        string pagePath = info.Arguments.Count > 0
            ? string.Join("/", info.Arguments)
            : "help";

        Debug.WriteLine($"[InfoCommand] Запрошен путь: {pagePath}");

        // Проверяем специальные команды (поиск)
        if (pagePath.StartsWith("search:"))
        {
            string query = pagePath.Substring(7);
            HandleSearch(query, userId);
            return;
        }

        // ✅ Просто загружаем страницу по пути через InfoService
        string response = _infoService.Render(pagePath, userId);

        msg.Message = "";
        msg.IsProcessedByCommand = true;
        msg.ShouldChargeForCommand = false;

        var webServer = WebServerService.Instance;
        if (webServer != null)
        {
            webServer.SendInfoMessage(response, pagePath);
            Debug.WriteLine($"[InfoCommand] Отправлено в /info/stream: {pagePath}");
        }
    }

    private void HandleSearch(string query, string userId)
    {
        var webServer = WebServerService.Instance;
        if (webServer != null)
        {
            // ✅ Передаем userId
            var results = _infoService.Search(query, userId);
            webServer.SendInfoMessage(results, "search");
        }
    }
}