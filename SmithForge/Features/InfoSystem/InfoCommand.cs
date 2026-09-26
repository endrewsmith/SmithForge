// Features/InfoSystem/InfoCommand.cs

using SmithForge.Features.InfoSystem;
using SmithForge.Features.TechOverlay;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.Main.Services.ChatCommands;
using System.Collections.Generic;
using System.Diagnostics;

public class InfoCommand : BaseCommand
{
    private readonly InfoService _infoService;
    private readonly StickerPageService _stickerPageService;

    public override bool IsTechnical => true;
    public override string Name => "info";
    public override IEnumerable<string> Aliases => new[] { "инфо", "справка", "help", "хелп", "h", "х", "помощь" };
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
        Debug.WriteLine($"[InfoCommand] ========== НАЧАЛО ==========");

        // Команда техническая — в основной чат и дашборд не идёт
        msg.Message = string.Empty;
        msg.IsProcessedByCommand = true;
        msg.ShouldChargeForCommand = false;

        string userId = chater.Id;

        // ✅ Склеиваем ВСЕ аргументы через "/" для создания пути любой глубины
        string pagePath = info.Arguments.Count > 0
            ? string.Join("/", info.Arguments)
            : "help";

        Debug.WriteLine($"[InfoCommand] Запрошен путь: {pagePath}");

        var webServer = WebServerService.Instance;
        if (webServer == null)
        {
            Debug.WriteLine($"[InfoCommand] ⚠️ WebServerService.Instance == null, ответ не отправлен");
            return;
        }

        // Проверяем специальные команды (поиск)
        if (pagePath.StartsWith("search:"))
        {
            string query = pagePath.Substring(7);
            var results = _infoService.Search(query, userId);
            webServer.SendInfoMessage(results, "search");
            Debug.WriteLine($"[InfoCommand] Отправлен поиск: '{query}'");
        }
        else
        {
            // ✅ Просто загружаем страницу по пути через InfoService
            string response = _infoService.Render(pagePath, userId);
            webServer.SendInfoMessage(response, pagePath);
            Debug.WriteLine($"[InfoCommand] Отправлено в /info/stream: {pagePath}");
        }

        // ✅ Отправляем тех-событие — что пользователь открыл страницу в инфо-панели
        WebServerService.Instance?.SendTechnicalEvent(
            TechEventFactory.Info(chater, pagePath));
        Debug.WriteLine($"[InfoCommand] ========== КОНЕЦ ==========");
    }
}