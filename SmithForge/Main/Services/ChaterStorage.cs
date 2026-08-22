using SmithForge.Main.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    public static class ChaterStorage
    {
        // 1. ГЛАВНЫЙ РЕЕСТР: Внутренний GUID -> Один единственный объект в памяти
        private static readonly ConcurrentDictionary<string, Chater> _allChaters = new();

        // 2. КАРТА ПОИСКА: ключ (платформа:логин) -> Тот же самый объект
        public static readonly ConcurrentDictionary<string, Chater> Cache = new();

        // 3. СОБЫТИЕ для уведомления об изменениях
        public static event Action<Chater>? OnChaterUpdated;

        // 4. КОНСТАНТА для кармы
        private const double DEFAULT_KARMA_PER_MESSAGE = 0;

        /// <summary>
        /// ЕДИНСТВЕННЫЙ МЕТОД AddOrUpdate - добавляет или обновляет чаттера в хранилище
        /// </summary>
        public static void AddOrUpdate(Chater chater)
        {
            if (chater == null) return;

            // Пытаемся получить существующего мастера по Id
            var master = _allChaters.GetOrAdd(chater.Id, chater);

            // Если это новый мастер (только что добавлен)
            if (master == chater)
            {
                // Обновляем кэш поиска для всех аккаунтов
                foreach (var acc in master.Accounts)
                {
                    if (!string.IsNullOrEmpty(acc.ExternalId))
                    {
                        Cache[acc.ExternalId.ToLower()] = master;
                    }
                }

                //Debug.WriteLine($"[ChaterStorage] Добавлен новый чаттер: {master.EffectiveName} (ID: {master.Id})");

                // Уведомляем о новом чаттере
                OnChaterUpdated?.Invoke(master);
                return;
            }

            // Если мастер уже существовал и мы обновляем данные
            bool wasUpdated = false;

            // Синхронизируем DisplayName, если он изменился и не пустой
            if (!string.IsNullOrEmpty(chater.DisplayName) && master.DisplayName != chater.DisplayName)
            {
                master.DisplayName = chater.DisplayName;
                wasUpdated = true;
                //Debug.WriteLine($"[ChaterStorage] Обновлен DisplayName для {master.Login}: {chater.DisplayName}");
            }

            // ✅ Синхронизируем IsDisplayNameCustom
            if (master.IsDisplayNameCustom != chater.IsDisplayNameCustom)
            {
                master.IsDisplayNameCustom = chater.IsDisplayNameCustom;
                wasUpdated = true;
                Debug.WriteLine($"[ChaterStorage] IsDisplayNameCustom обновлён: {master.IsDisplayNameCustom} для {master.Login}");
            }

            // Синхронизируем Login (технический), если он изменился
            if (!string.IsNullOrEmpty(chater.Login) && master.Login != chater.Login)
            {
                master.Login = chater.Login;
                wasUpdated = true;
            }

            // Обновляем статистику
            if (master.MessageCount != chater.MessageCount)
            {
                master.MessageCount = chater.MessageCount;
                wasUpdated = true;
            }

            if (Math.Abs(master.Karma - chater.Karma) > 0.001)
            {
                master.Karma = chater.Karma;
                wasUpdated = true;
            }

            if (Math.Abs(master.TotalKarma - chater.TotalKarma) > 0.001)
            {
                master.TotalKarma = chater.TotalKarma;
                wasUpdated = true;
            }

            if (master.Rank != chater.Rank)
            {
                master.Rank = chater.Rank;
                wasUpdated = true;
            }

            if (master.LastMessageTime != chater.LastMessageTime)
            {
                master.LastMessageTime = chater.LastMessageTime;
                wasUpdated = true;
            }

            // Проверяем новые аккаунты
            foreach (var acc in chater.Accounts)
            {
                if (!master.Accounts.Any(x => x.ExternalId == acc.ExternalId))
                {
                    master.Accounts.Add(acc);
                    if (!string.IsNullOrEmpty(acc.ExternalId))
                    {
                        Cache[acc.ExternalId.ToLower()] = master;
                    }
                    wasUpdated = true;
                    Debug.WriteLine($"[ChaterStorage] Добавлен аккаунт {acc.Platform}:{acc.OriginalName}");
                }
            }

            // Если были изменения - уведомляем
            if (wasUpdated)
            {
                OnChaterUpdated?.Invoke(master);
                // НЕ сохраняем в БД автоматически - это должен делать вызывающий код
            }
        }

        /// <summary>
        /// Обновление или создание чаттера из входящего сообщения
        /// </summary>
        public static Chater UpdateFromMessage(CommonMessage msg, AppSettings settings)
        {
            string key = $"{msg.Type}:{msg.Login}".ToLower();

            // 1. Проверяем кэш
            if (Cache.TryGetValue(key, out var chater))
            {
                chater.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DatabaseService.UpdateChaterStats(chater);
                return chater;
            }

            // 2. Проверяем БД
            var dbChater = DatabaseService.LoadChater(key);
            if (dbChater != null)
            {
                AddOrUpdate(dbChater);
                dbChater.LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                DatabaseService.UpdateChaterStats(dbChater);
                return dbChater;
            }

            // 3. Создаём нового пользователя
            var newChater = new Chater
            {
                Id = Guid.NewGuid().ToString(),
                Login = msg.Login,
                DisplayName = msg.Login, // ✅ Исправлено: не пустая строка, а Login
                FirstSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                LastMessageTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                MessageCount = 0,
                Karma = 0,
                TotalKarma = 0,
                Rank = 0,
                AvatarFileName = "default.png",
                IsDisplayNameCustom = false // Новые пользователи имеют авто-имя
            };

            // Добавляем внешний аккаунт
            newChater.Accounts.Add(new ExternalAccount
            {
                ExternalId = key,
                Platform = msg.Type,
                OriginalName = msg.Login
            });

            DatabaseService.SaveChater(newChater);
            AddOrUpdate(newChater);

            Debug.WriteLine($"[ChaterStorage] Создан новый чаттер: {msg.Login} на платформе {msg.Type}");

            return newChater;
        }

        /// <summary>
        /// Миграция аккаунтов: обновляет короткие имена на ID каналов
        /// Вызывается при загрузке пользователя из БД
        /// </summary>
        public static void MigrateAccounts(Chater chater)
        {
            foreach (var account in chater.Accounts.ToList())
            {
                // Проверяем, что это YouTube/Twitch аккаунт с коротким именем
                var isShortName = account.ExternalId.Contains(":@") || 
                                  account.ExternalId.Contains(":Smith") ||
                                  account.ExternalId.Contains(":smith");

                if (isShortName && !account.ExternalId.Contains(":UC") && !account.ExternalId.Contains(":Twitch"))
                {
                    // Это короткое имя — нужно заменить на ID канала
                    // Но у нас нет ID канала, поэтому просто оставляем как есть
                    // ID канала добавится при следующем сообщении через ProcessConnectorMessage
                }
            }
        }

        /// <summary>
        /// Удаление чаттера по ExternalId
        /// </summary>
        public static void Remove(string extId)
        {
            if (string.IsNullOrEmpty(extId)) return;

            if (Cache.TryRemove(extId.ToLower(), out var chater))
            {
                _allChaters.TryRemove(chater.Id, out _);

                foreach (var a in chater.Accounts)
                {
                    if (!string.IsNullOrEmpty(a.ExternalId))
                    {
                        Cache.TryRemove(a.ExternalId.ToLower(), out _);
                    }
                }

                DatabaseService.DeleteChater(chater.Id);

                Debug.WriteLine($"[ChaterStorage] Удален чаттер: {chater.EffectiveName} (ID: {chater.Id})");
            }
        }

        /// <summary>
        /// Получение чаттера по ExternalId
        /// </summary>
        public static Chater? GetByExternalId(string extId)
        {
            if (string.IsNullOrEmpty(extId)) return null;

            Cache.TryGetValue(extId.ToLower(), out var chater);
            return chater;
        }

        /// <summary>
        /// Получение чаттера по Id
        /// </summary>
        public static Chater? GetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;

            _allChaters.TryGetValue(id, out var chater);
            return chater;
        }

        /// <summary>
        /// Получение всех чаттеров
        /// </summary>
        public static List<Chater> GetAll()
        {
            return _allChaters.Values.ToList();
        }

        /// <summary>
        /// Загрузка всех чаттеров из БД в кэш
        /// </summary>
        public static void LoadAllFromDatabase()
        {
            var allChaters = DatabaseService.LoadAll();

            foreach (var chater in allChaters)
            {
                AddOrUpdate(chater);
            }

            //Debug.WriteLine($"[ChaterStorage] Загружено {allChaters.Count} чаттеров из БД");
        }

        /// <summary>
        /// Очистка хранилища (для тестов или перезагрузки)
        /// </summary>
        public static void Clear()
        {
            _allChaters.Clear();
            Cache.Clear();
            Debug.WriteLine("[ChaterStorage] Хранилище очищено");
        }

        /// <summary>
        /// Удаление чаттера по ID
        /// </summary>
        public static void RemoveById(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            if (_allChaters.TryRemove(id, out var chater))
            {
                // Удаляем все его аккаунты из кэша
                foreach (var a in chater.Accounts)
                {
                    if (!string.IsNullOrEmpty(a.ExternalId))
                    {
                        Cache.TryRemove(a.ExternalId.ToLower(), out _);
                    }
                }

                Debug.WriteLine($"[ChaterStorage] Удален чаттер по ID: {chater.EffectiveName} (ID: {chater.Id})");
            }
        }
    }
}