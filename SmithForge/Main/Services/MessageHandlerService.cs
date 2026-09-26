using SmithForge.ChatEngine.Core.Models;
using SmithForge.Main.Models;
using SmithForge.Main.Services.ChatCommands;
using SmithForge.ViewModels;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.IO;

namespace SmithForge.Main.Services
{
    public class MessageHandlerService
    {

        private readonly WebServerService? _webServer;
        private readonly MessageProcessor _processor;
        private readonly OverlayManagerService _overlayManager;
        private readonly DashboardService _dashboardService; // ← ДОБАВЛЕНО
        private readonly SmithForge.Features.MediaDashboard.MediaDashboardService _mediaDashboard;

        // Кэш для дедупликации сообщений (ключ = connectorId:platform:userId:text:timestamp_second)
        private readonly ConcurrentDictionary<string, DateTime> _processedMessageCache = new();
        private const int DedupCacheSeconds = 10; // Храним 10 секунд

        public event Action<Chater, CommonMessage, List<ChatCommandInfo>>? OnProcessed;

        internal MessageHandlerService(
            MessageProcessor processor,
            OverlayManagerService overlayManager,
            DashboardService dashboardService,
            SmithForge.Features.MediaDashboard.MediaDashboardService mediaDashboard,
            WebServerService? webServer = null)
        {
            _processor = processor;
            _overlayManager = overlayManager;
            _dashboardService = dashboardService;
            _mediaDashboard = mediaDashboard;
            _webServer = webServer;
            _processor.OnProcessed += OnMessageProcessed;
        }

        public void ProcessMessage(Chater chater, CommonMessage msg, List<ChatCommandInfo>? commands)
        {
            OnProcessed?.Invoke(chater, msg, commands ?? new());
        }

        public void ProcessExternalMessage(CommonMessage msg)
        {
            Debug.WriteLine("═══════════════════════════════════════════════");
            Debug.WriteLine("[PATH] ProcessExternalMessage (WebSocket)");
            Debug.WriteLine($"   Type: {msg.Type}");
            Debug.WriteLine($"   Login: {msg.Login}");
            Debug.WriteLine($"   ChannelId: {msg.ChannelId}");
            Debug.WriteLine("═══════════════════════════════════════════════");

            // Для сообщений из внешнего источника (MessageService)
            // Создаём пустой список команд
            var commands = new List<ChatCommandInfo>();

            // Обработка через _processor
            var chater = ChaterStorage.UpdateFromMessage(msg, new AppSettings());
            msg.User = chater;

            _processor.Process(msg);
        }

        private void OnMessageProcessed(Chater chater, CommonMessage msg, List<ChatCommandInfo> commands)
        {
            try
            {
                Debug.WriteLine($"[MessageHandler] Получено сообщение от {chater.Login}:");
                Debug.WriteLine($"   - Оригинальный номер: {msg.MessageNumber}");
                Debug.WriteLine($"   - Текст: {msg.Message}");
                Debug.WriteLine($"   - IsProcessedByCommand: {msg.IsProcessedByCommand}");
                Debug.WriteLine($"   - IsVisible: {msg.IsVisible}");

                // ✅ СЛУЖЕБНЫЕ (номер == 0) — чисто сервисные команды (!!nick, !!ava, !!like, !!dislike).
                if (msg.MessageNumber == 0)
                {
                    Debug.WriteLine($"[MessageHandler] 🔧 Служебное сообщение (ID=0), полный отсев");
                    return;
                }

                bool isVoiceAction = msg.Message.Contains("<voice>") || msg.Message.Contains("</voice>");
                bool isHideAction = msg.Message.Contains("<hide>") || msg.Message.Contains("</hide>");
                bool isStickerAction = msg.Message.Contains("<sticker");
                bool isVideoAction = msg.Message.Contains("<video");
                bool isReactionAction = msg.Message.Contains("<like") || msg.Message.Contains("<dislike") || msg.Message.Contains("<nick");
                bool hasFormatting = msg.Message.Contains("<b>") || msg.Message.Contains("</b>") ||
                                     msg.Message.Contains("<i>") || msg.Message.Contains("</i>") ||
                                     msg.Message.Contains("<color=") || msg.Message.Contains("</color>") ||
                                     msg.Message.Contains("<c=") || msg.Message.Contains("</c>");

                if (string.IsNullOrWhiteSpace(msg.Message))
                {
                    Debug.WriteLine($"[MessageHandler] ⏭ Сообщение пустое, пропускаем");
                    return;
                }

                string cleanUiMessage = msg.Message;
                cleanUiMessage = System.Text.RegularExpressions.Regex.Replace(
                    cleanUiMessage,
                    @"</?(?:voice|hide|sticker|like|dislike|nick|sound)[^>]*>",
                    "");
                cleanUiMessage = cleanUiMessage.Trim();

                var overlayMsg = new CommonMessage
                {
                    User = chater,
                    Login = chater.Login,
                    Type = msg.Type.ToLower(),
                    Message = cleanUiMessage,
                    KarmaKeyDisplay = $"#{chater.KarmaKey}",
                    MessageNumber = msg.MessageNumber,
                    IsProcessedByCommand = msg.IsProcessedByCommand,
                    DisplayTimeMs = msg.DisplayTimeMs,
                    IsVisible = msg.IsVisible
                };

                // ════════════════════════════════════════════════════════════
                // ✅ 1. В ДАШБОРД
                // ════════════════════════════════════════════════════════════
                if (msg.IsVisible)
                {
                    _dashboardService.AddMessage(chater, overlayMsg);
                    Debug.WriteLine($"[Dashboard] ✅ Добавлено: {chater.EffectiveName}: {cleanUiMessage}");
                }
                else
                {
                    Debug.WriteLine($"[Dashboard] ⏭ Служебное (IsVisible=false) НЕ идёт в дашборд: {cleanUiMessage}");
                }

                // ════════════════════════════════════════════════════════════
                // ✅ 2. ТЕХНИЧЕСКИЕ ДЕЙСТВИЯ — ЕДИНАЯ ЦЕПОЧКА if / else if
                // ════════════════════════════════════════════════════════════
                if (isVoiceAction)
                {
                    Debug.WriteLine($"[Voice] ✅ Голосовое сообщение от {chater.Login}: {cleanUiMessage}");

                    if (!string.IsNullOrWhiteSpace(cleanUiMessage))
                    {
                        Debug.WriteLine($"[Voice] 🎙️ Прямой вызов SayAsync('{cleanUiMessage}')");
                        Task.Run(() => VoiceService.SayAsync(cleanUiMessage));
                    }
                    else
                    {
                        Debug.WriteLine($"[Voice] ⏭ Текст пустой, нечего озвучивать");
                    }
                }
                else if (isStickerAction)
                {
                    Debug.WriteLine($"[Stickers] ✅ Стикер от {chater.Login}");

                    _overlayManager.AddStickerMessage(chater, overlayMsg);

                    Task.Run(() =>
                    {
                        try
                        {
                            var stickerMatch = System.Text.RegularExpressions.Regex.Match(
                                msg.Message,
                                @"<sticker pack='(\d+)' id='(\d+)' path='([^']+)'");

                            if (!stickerMatch.Success) return;

                            string stickerPath = stickerMatch.Groups[3].Value;
                            string stickerId = stickerMatch.Groups[2].Value;
                            bool isAnimated =
                                stickerPath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                stickerPath.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

                            string cleanText = System.Text.RegularExpressions.Regex.Replace(
                                msg.Message, @"<sticker[^>]*/>", "").Trim();

                            _webServer?.SendStickerToMedia(
                                chater.EffectiveName, stickerPath, stickerId, isAnimated, cleanText);

                            _mediaDashboard.AddMedia(new SmithForge.Features.MediaDashboard.MediaDashboardItem
                            {
                                MediaType = "sticker",
                                UserName = chater.EffectiveName,
                                FilePath = stickerPath,
                                IsAnimated = isAnimated,
                                Text = cleanText,
                                Timestamp = DateTime.Now
                            });

                            Debug.WriteLine($"[MediaDashboard] ✅ Стикер добавлен: {stickerId}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Media] ❌ Ошибка отправки стикера: {ex.Message}");
                        }
                    });
                }
                else if (isVideoAction)
                {
                    Debug.WriteLine($"[Video] ✅ Видео от {chater.Login}");

                    Task.Run(() =>
                    {
                        try
                        {
                            var videoMatch = System.Text.RegularExpressions.Regex.Match(
                                msg.Message, @"<video path='([^']+)' />");

                            if (!videoMatch.Success) return;

                            string videoPath = videoMatch.Groups[1].Value;

                            string webPath = videoPath.Replace(
                                AppDomain.CurrentDomain.BaseDirectory, "/")
                                .Replace("\\", "/")
                                .Replace("//", "/");

                            if (!webPath.StartsWith("/SF_Data/"))
                                webPath = "/SF_Data/" + webPath.TrimStart('/');

                            string extension = Path.GetExtension(videoPath).ToLower();
                            bool isAnimatedGif = extension == ".gif" || extension == ".webp";

                            var json = new
                            {
                                type = isAnimatedGif ? "sticker" : "video",
                                userName = chater.EffectiveName,
                                videoUrl = webPath,
                                stickerPath = webPath,
                                isAnimated = isAnimatedGif,
                                text = "",
                                timestamp = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss")
                            };
                            _webServer?.SendMediaMessage(System.Text.Json.JsonSerializer.Serialize(json));

                            _mediaDashboard.AddMedia(new SmithForge.Features.MediaDashboard.MediaDashboardItem
                            {
                                MediaType = isAnimatedGif ? "sticker" : "video",
                                UserName = chater.EffectiveName,
                                FilePath = videoPath,
                                WebUrl = webPath,
                                IsAnimated = isAnimatedGif,
                                Text = "",
                                Timestamp = DateTime.Now
                            });

                            Debug.WriteLine($"[MediaDashboard] ✅ Видео добавлено: {webPath}");
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[Media] ❌ Ошибка отправки видео: {ex.Message}");
                        }
                    });
                }
                else if (isReactionAction)
                {
                    Debug.WriteLine($"[Reaction] Реакция от {chater.Login}: {msg.Message}");

                    var likeMatch = System.Text.RegularExpressions.Regex.Match(
                        msg.Message, @"<like\s+msg='(\d+)'\s+user='([^']+)'");

                    var dislikeMatch = System.Text.RegularExpressions.Regex.Match(
                        msg.Message, @"<dislike\s+msg='(\d+)'\s+user='([^']+)'");

                    if (likeMatch.Success)
                    {
                        int msgNumber = int.Parse(likeMatch.Groups[1].Value);
                        string userId = likeMatch.Groups[2].Value;
                        HandleReaction(msgNumber, userId, "like");
                    }
                    else if (dislikeMatch.Success)
                    {
                        int msgNumber = int.Parse(dislikeMatch.Groups[1].Value);
                        string userId = dislikeMatch.Groups[2].Value;
                        HandleReaction(msgNumber, userId, "dislike");
                    }
                }
                else if (isHideAction)
                {
                    Debug.WriteLine($"[Hide] 📝 Скрытое сообщение от {chater.Login}: {cleanUiMessage}");
                }
                else if (hasFormatting || !string.IsNullOrEmpty(cleanUiMessage))
                {
                    _overlayManager.AddMessage(chater, overlayMsg);
                }

                // ════════════════════════════════════════════════════════════
                // ✅ 3. В ВЕБ-ОВЕРЛЕЙ — ОТДЕЛЬНО, ПОСЛЕ ВСЕЙ ЦЕПОЧКИ
                // ════════════════════════════════════════════════════════════
                if (_webServer != null && msg.IsVisible)
                {
                    var displayMsg = new DisplayMessageViewModel(chater, overlayMsg);
                    _webServer.AddMessage(displayMsg);
                    Debug.WriteLine($"[WebServer] ✅ Добавлено в веб: {chater.EffectiveName}: {cleanUiMessage}");
                }
                else if (!msg.IsVisible)
                {
                    Debug.WriteLine($"[WebServer] ⏭ Служебное (IsVisible=false) НЕ идёт в веб-чат");
                }

                // ✅ Уведомляем ротацию об активности
                if (Application.Current.MainWindow?.DataContext is MainViewModel mainVm)
                {
                    mainVm.NotifyUserActivity();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MessageHandler] Ошибка обработки: {ex.Message}");
            }
        }
        /// <summary>
        /// Обработка реакции (лайк/дизлайк): запись в БД и уведомление веб-оверлея
        /// </summary>
        private void HandleReaction(int messageNumber, string userId, string reactionType)
        {
            try
            {
                Debug.WriteLine($"[Reaction] Обработка: #{messageNumber}, user={userId}, type={reactionType}");

                // 1. Находим ChatLogs.Id по номеру сообщения
                long chatLogId = DatabaseService.GetMessageIdByNumber(messageNumber);
                if (chatLogId <= 0)
                {
                    Debug.WriteLine($"[Reaction] ⚠️ Сообщение #{messageNumber} не найдено в БД");
                    return;
                }

                Debug.WriteLine($"[Reaction] ChatLogs.Id = {chatLogId}");

                // 2. Ставим реакцию в БД
                if (reactionType == "like")
                    DatabaseService.LikeMessage(chatLogId, userId);
                else
                    DatabaseService.DislikeMessage(chatLogId, userId);

                // 3. Получаем актуальные счётчики
                var counts = DatabaseService.GetReactionCounts(chatLogId);

                // 4. Отправляем обновление в веб-оверлей
                int newCount = reactionType == "like" ? counts.Likes : counts.Dislikes;

                WebServerService.Instance?.UpdateReactionInWeb(
                    messageNumber: messageNumber,
                    reactionType: reactionType,
                    newCount: newCount);

                Debug.WriteLine($"[Reaction] ✅ {reactionType} для #{messageNumber}: likes={counts.Likes}, dislikes={counts.Dislikes}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Reaction] ❌ Ошибка: {ex.Message}");
                Debug.WriteLine($"[Reaction] StackTrace: {ex.StackTrace}");
            }
        }
        public void ProcessConnectorMessage(object? sender, IncomingChatMessage message)
        {
            Debug.WriteLine($"[MessageHandler] ProcessConnectorMessage: {message.UserName}: {message.Text}");
            Debug.WriteLine("═══════════════════════════════════════════════");
            Debug.WriteLine("[PATH] ProcessConnectorMessage");
            Debug.WriteLine($"   ConnectorId: {message.ConnectorId}");
            Debug.WriteLine($"   Platform: {message.Platform}");
            Debug.WriteLine($"   UserId (ID канала): {message.UserId}");
            Debug.WriteLine($"   UserName (ник): {message.UserName}");
            Debug.WriteLine($"   Text: {message.Text}");
            Debug.WriteLine($"   ChannelId: {message.ChannelId}");
            Debug.WriteLine("═══════════════════════════════════════════════");

            try
            {
                // ✅ Дедупликация: ключ включает ConnectorId для разных чатов
                var timestampSec = (long)(message.Timestamp.ToUniversalTime() - new DateTime(1970, 1, 1)).TotalSeconds;
                var dedupKey = $"{message.ConnectorId}:{message.Platform}:{message.UserId}:{message.Text.ToLower().Trim()}:{timestampSec}";

                // Проверяем, обрабатывали ли уже это сообщение
                if (_processedMessageCache.ContainsKey(dedupKey))
                {
                    Debug.WriteLine($"[MessageHandler] ДУБЛИКАТ пропущен: {message.UserName}: {message.Text}");
                    return;
                }

                // Добавляем в кэш
                _processedMessageCache[dedupKey] = DateTime.UtcNow;

                // Очищаем старые записи (старше 10 секунд)
                var now = DateTime.UtcNow;
                var expiredKeys = _processedMessageCache
                    .Where(kvp => (now - kvp.Value).TotalSeconds > DedupCacheSeconds)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var key in expiredKeys)
                {
                    _processedMessageCache.TryRemove(key, out _);
                }

                // Нормализуем имя: убираем @ в начале
                string normalizedName = message.UserName.TrimStart('@').Trim();
                if (string.IsNullOrEmpty(normalizedName)) normalizedName = message.UserName;

                Debug.WriteLine($"[MessageHandler] Получено сообщение от {normalizedName}: {message.Text}");
                Debug.WriteLine($"[MessageHandler]   UserId (ID канала): '{message.UserId}'");
                Debug.WriteLine($"[MessageHandler]   UserName (ник): '{message.UserName}'");

                var commonMsg = new CommonMessage
                {
                    Type = message.Platform.ToString().ToLower(),
                    Login = message.UserName,
                    Message = message.Text,
                    Timestamp = message.Timestamp.Ticks,
                    ChannelId = message.ChannelId ?? message.UserId
                };

                // ✅ ГЛАВНЫЙ КЛЮЧ: ID канала (не меняется!)
                var externalIdByChannelId = $"{message.Platform}:{message.UserId}";
                Debug.WriteLine($"[MessageHandler] Поиск по ID канала: '{externalIdByChannelId}'");
                commonMsg.User = ChaterStorage.GetByExternalId(externalIdByChannelId);

                // ✅ Если не нашли по ID канала, ищем по короткому имени (для обратной совместимости)
                if (commonMsg.User == null && !string.IsNullOrEmpty(message.UserName))
                {
                    var externalIdByShortName = $"{message.Platform}:{message.UserName}".ToLower();
                    Debug.WriteLine($"[MessageHandler] Поиск по короткому имени: '{externalIdByShortName}'");
                    commonMsg.User = ChaterStorage.GetByExternalId(externalIdByShortName);

                    // Если нашли по короткому имени — обновляем аккаунт на ID канала
                    if (commonMsg.User != null)
                    {
                        Debug.WriteLine($"[MessageHandler] Найден по короткому имени, обновляем на ID канала");

                        // Удаляем старый аккаунт с коротким именем
                        var shortNameAccount = commonMsg.User.Accounts
                            .FirstOrDefault(a => a.ExternalId == externalIdByShortName);
                        if (shortNameAccount != null)
                        {
                            commonMsg.User.Accounts.Remove(shortNameAccount);
                            Debug.WriteLine($"[MessageHandler] Удалён старый аккаунт: {externalIdByShortName}");
                        }

                        // Добавляем аккаунт с ID канала (если ещё нет)
                        if (!commonMsg.User.Accounts.Any(a => a.ExternalId == externalIdByChannelId))
                        {
                            commonMsg.User.Accounts.Add(new ExternalAccount
                            {
                                ExternalId = externalIdByChannelId,
                                Platform = message.Platform.ToString().ToLower(),
                                OriginalName = message.UserName
                            });
                            Debug.WriteLine($"[MessageHandler] Добавлен ID канала: {externalIdByChannelId}");
                        }

                        commonMsg.User.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                        ChaterStorage.AddOrUpdate(commonMsg.User);
                        DatabaseService.SaveChater(commonMsg.User);
                    }
                }

                // Если пользователь не найден — создаём нового
                if (commonMsg.User == null)
                {
                    Debug.WriteLine($"[MessageHandler] НОВЫЙ ПОЛЬЗОВАТЕЛЬ! Создаём с ID канала: '{externalIdByChannelId}'");

                    commonMsg.User = new Chater
                    {
                        Id = Guid.NewGuid().ToString(),
                        Login = normalizedName,
                        DisplayName = normalizedName,
                        FirstSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        MessageCount = 0,
                        Karma = 0,
                        TotalKarma = 0,
                        Rank = 0,
                        AvatarFileName = "default.png",
                        IsDisplayNameCustom = false // Новые пользователи имеют авто-имя
                    };

                    // ✅ ГЛАВНЫЙ ключ — ID канала (не меняется при смене ника)
                    commonMsg.User.Accounts.Add(new ExternalAccount
                    {
                        ExternalId = externalIdByChannelId,
                        Platform = message.Platform.ToString().ToLower(),
                        OriginalName = message.UserName
                    });

                    ChaterStorage.AddOrUpdate(commonMsg.User);
                    DatabaseService.SaveChater(commonMsg.User);

                    Debug.WriteLine($"[MessageHandler] Новый пользователь создан с ID: {commonMsg.User.Id}");
                    Debug.WriteLine($"[MessageHandler]   ExternalId: {externalIdByChannelId}");
                    Debug.WriteLine($"[MessageHandler]   DisplayName: {commonMsg.User.DisplayName}");
                    Debug.WriteLine($"[MessageHandler]   IsDisplayNameCustom: {commonMsg.User.IsDisplayNameCustom}");
                }
                else
                {
                    // ✅ Пользователь уже есть — проверяем наличие ID канала в аккаунтах
                    var hasChannelId = commonMsg.User.Accounts.Any(a => a.ExternalId == externalIdByChannelId);

                    if (!hasChannelId)
                    {
                        Debug.WriteLine($"[MessageHandler] Добавляем ID канала для существующего пользователя: {externalIdByChannelId}");

                        // Удаляем все аккаунты с короткими именами (содержат @)
                        var accountsToRemove = commonMsg.User.Accounts
                            .Where(a => a.ExternalId.Contains(":@"))
                            .ToList();

                        foreach (var acc in accountsToRemove)
                        {
                            Debug.WriteLine($"[MessageHandler] Удаляем старый аккаунт: {acc.ExternalId}");
                            commonMsg.User.Accounts.Remove(acc);
                        }

                        // Добавляем ID канала как ГЛАВНЫЙ ключ
                        commonMsg.User.Accounts.Insert(0, new ExternalAccount
                        {
                            ExternalId = externalIdByChannelId,
                            Platform = message.Platform.ToString().ToLower(),
                            OriginalName = message.UserName
                        });

                        Debug.WriteLine($"[MessageHandler] Добавлен ID канала {message.UserId} для пользователя {normalizedName}");
                    }

                    // ✅ ЛОГ: состояние ДО обновления DisplayName
                    Debug.WriteLine($"[MessageHandler] ДО обновления: User={commonMsg.User.Login}, DisplayName='{commonMsg.User.DisplayName}', IsDisplayNameCustom={commonMsg.User.IsDisplayNameCustom}");

                    // ✅ НЕ перезаписываем DisplayName, если он установлен вручную
                    if (!commonMsg.User.IsDisplayNameCustom)
                    {
                        // Обновляем DisplayName на актуальный ник (если изменился)
                        if (!string.Equals(commonMsg.User.DisplayName, normalizedName, StringComparison.OrdinalIgnoreCase))
                        {
                            commonMsg.User.DisplayName = normalizedName;
                            Debug.WriteLine($"[MessageHandler] ✅ DisplayName ОБНОВЛЁН (авто): '{commonMsg.User.DisplayName}'");
                        }
                        else
                        {
                            Debug.WriteLine($"[MessageHandler] ⏭ DisplayName НЕ ИЗМЕНИЛСЯ: '{commonMsg.User.DisplayName}'");
                        }
                    }
                    else
                    {
                        Debug.WriteLine($"[MessageHandler] 🚫 DisplayName НЕ ОБНОВЛЁН (пользовательское): '{commonMsg.User.DisplayName}'");
                    }

                    // ✅ ЛОГ: состояние ПОСЛЕ обновления DisplayName
                    Debug.WriteLine($"[MessageHandler] ПОСЛЕ обновления: User={commonMsg.User.Login}, DisplayName='{commonMsg.User.DisplayName}', IsDisplayNameCustom={commonMsg.User.IsDisplayNameCustom}");

                    // Обновляем время последнего сообщения
                    commonMsg.User.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                    // Сохраняем обновления в базу
                    ChaterStorage.AddOrUpdate(commonMsg.User);
                    DatabaseService.SaveChater(commonMsg.User);

                    Debug.WriteLine($"[MessageHandler] Аккаунты пользователя: {string.Join(", ", commonMsg.User.Accounts.Select(a => a.ExternalId))}");
                }

                // Передаём сообщение в процессор
                _processor.Process(commonMsg);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[MessageHandler] Ошибка обработки сообщения: {ex.Message}");
                Debug.WriteLine($"[MessageHandler] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Очистка кэша дедупликации
        /// </summary>
        public void ClearCache()
        {
            _processedMessageCache.Clear();
            Debug.WriteLine("[MessageHandler] Кэш дедупликации очищен");
        }

        public void SetSession(string sessionId)
        {
            _processor.SetSession(sessionId);
            Debug.WriteLine($"[MessageHandlerService] Сессия установлена: {sessionId}");
        }
    }
}