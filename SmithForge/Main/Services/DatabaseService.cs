using Dapper;
using Microsoft.Data.Sqlite;
using SmithForge.Main.Models;
using SmithForge.Main.Services.SmithForge.Main.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace SmithForge.Main.Services
{
    public static class DatabaseService
    {
        private static string ConnectionString => $"Data Source={FolderManager.GetDbPath()}";

        public static void Initialize()
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            db.Execute(Sql.InitTables);

            // Инициализация таблицы реакций
            InitializeReactionsTable();

            db.Execute("CREATE INDEX IF NOT EXISTS idx_external_chater ON ExternalAccounts(ExternalId)");
        }

        // --- ПОИСК И ЗАГРУЗКА ---

        private static Chater? QueryChater(string sql, object param)
        {
            using var db = new SqliteConnection(ConnectionString);
            var dict = new Dictionary<string, Chater>();

            db.Query<Chater, ExternalAccount, Chater>(sql, (c, a) =>
            {
                if (!dict.TryGetValue(c.Id, out var entry))
                    dict.Add(c.Id, entry = c);

                if (a != null && !entry.Accounts.Any(x => x.ExternalId == a.ExternalId))
                    entry.Accounts.Add(a);

                return entry;
            }, param, splitOn: "ExternalId");

            var result = dict.Values.FirstOrDefault();

            if (result != null) ChaterStorage.AddOrUpdate(result);

            return result;
        }

        public static Chater? LoadChater(string extId) => QueryChater(Sql.LoadByExtId, new { extId });
        public static Chater? GetChaterByKarmaKey(int k) => QueryChater(Sql.LoadByKarmaKey, new { k });

        public static List<Chater> LoadAll()
        {
            using var db = new SqliteConnection(ConnectionString);
            var dict = new Dictionary<string, Chater>();
            db.Query<Chater, ExternalAccount, Chater>(Sql.LoadAll, (c, a) =>
            {
                if (!dict.TryGetValue(c.Id, out var entry))
                    dict.Add(c.Id, entry = c);
                if (a != null && !entry.Accounts.Any(x => x.ExternalId == a.ExternalId))
                    entry.Accounts.Add(a);
                return entry;
            }, splitOn: "ExternalId");

            var results = dict.Values.ToList();
            foreach (var r in results) ChaterStorage.AddOrUpdate(r);

            return results;
        }

        // --- СОХРАНЕНИЕ ---

        public static void SaveChater(Chater c)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            using var tx = db.BeginTransaction();
            try
            {
                db.Execute(Sql.SaveChaterFull, new
                {
                    c.Id,
                    c.PersonId,
                    Name = c.Login,
                    c.DisplayName,
                    c.AvatarFileName,
                    c.Rank,
                    KarmaMultiplier = 1.0 + (c.Rank * 0.1),
                    Karma = c.Karma,
                    TotalKarma = c.TotalKarma,
                    c.MessageCount,
                    c.FirstSeen,
                    c.LastMessageTime,
                    c.MessageXamlTemplate,
                    CustomKarmaKey = c.IsKarmaKeyPermanent ? (int?)c.KarmaKey : null,
                    IsKeyPermanent = c.IsKarmaKeyPermanent ? 1 : 0
                }, tx);

                foreach (var acc in c.Accounts)
                {
                    db.Execute(Sql.SaveExternal, new { acc.ExternalId, ChaterId = c.Id, acc.Platform, Name = acc.OriginalName }, tx);
                }
                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine($"[DB ERROR] SaveChater: {ex.Message}");
                throw;
            }
        }

        public static void UpdateChaterStats(Chater c)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Execute(Sql.UpdateChaterStats, new
            {
                c.Id,
                Karma = c.Karma,
                TotalKarma = c.TotalKarma,
                c.MessageCount,
                c.LastMessageTime,
                c.Rank
            });
        }

        public static void DeleteChater(string chaterId)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Execute("DELETE FROM Chaters WHERE Id = @chaterId", new { chaterId });
        }

        public static void DeleteExternalAccount(string externalId)
        {
            if (string.IsNullOrEmpty(externalId)) return;

            using var db = new SqliteConnection(ConnectionString);
            int rowsAffected = db.Execute("DELETE FROM ExternalAccounts WHERE ExternalId = @externalId", new { externalId });

            if (rowsAffected > 0)
                Debug.WriteLine($"[DB] Удален ExternalAccount: {externalId}");
            else
                Debug.WriteLine($"[DB] ExternalAccount {externalId} не найден");
        }

        // --- СТРИМЫ И ЛОГИ ---

        public static List<StreamSession> GetAllSessions() =>
            new SqliteConnection(ConnectionString).Query<StreamSession>("SELECT * FROM StreamSessions ORDER BY StartTime DESC").ToList();

        public static void SaveSession(StreamSession s) =>
            new SqliteConnection(ConnectionString).Execute(Sql.SaveSession, s);

        // НОВЫЙ МЕТОД: Сохранение сообщения с автоматической нумерацией
        public static void SaveChatMessage(ChatLogMessage msg)
        {
            using var db = new SqliteConnection(ConnectionString);

            // Получаем следующий номер сообщения для этого стрима
            int nextNumber = GetNextMessageNumber(msg.SessionId);
            msg.MessageNumber = nextNumber;

            Debug.WriteLine($"[DB] Сохраняем сообщение #{nextNumber} для стрима {msg.SessionId}");

            db.Execute(@"
                INSERT INTO ChatLogs (SessionId, ChaterId, Message, Timestamp, MessageNumber, Likes, Dislikes)
                VALUES (@SessionId, @ChaterId, @Message, @Timestamp, @MessageNumber, @Likes, @Dislikes)",
                msg);
        }

        // НОВЫЙ МЕТОД: Получение следующего номера сообщения для стрима
        public static int GetNextMessageNumber(string sessionId)
        {
            using var db = new SqliteConnection(ConnectionString);
            return db.ExecuteScalar<int>(
                "SELECT COALESCE(MAX(MessageNumber), 0) + 1 FROM ChatLogs WHERE SessionId = @sessionId",
                new { sessionId });
        }

        // НОВЫЙ МЕТОД: Получение всех сообщений стрима
        public static List<ChatLogMessage> GetSessionMessages(string sessionId)
        {
            using var db = new SqliteConnection(ConnectionString);
            return db.Query<ChatLogMessage>(
                "SELECT * FROM ChatLogs WHERE SessionId = @sessionId ORDER BY MessageNumber ASC",
                new { sessionId }).ToList();
        }

        // НОВЫЙ МЕТОД: Удаление стрима и его сообщений
        public static void DeleteSession(string sessionId)
        {
            using var db = new SqliteConnection(ConnectionString);

            // Сначала удаляем все сообщения стрима
            db.Execute("DELETE FROM ChatLogs WHERE SessionId = @sessionId", new { sessionId });

            // Затем удаляем сам стрим
            db.Execute("DELETE FROM StreamSessions WHERE Id = @sessionId", new { sessionId });

            Debug.WriteLine($"[DB] Удален стрим {sessionId} и его сообщения");
        }

        public static StreamSession? GetActiveSession() =>
            new SqliteConnection(ConnectionString).QueryFirstOrDefault<StreamSession>(Sql.GetActiveSession);

        public static int GetMaxStreamNumber() =>
            new SqliteConnection(ConnectionString).ExecuteScalar<int>("SELECT COALESCE(MAX(Number), 0) FROM StreamSessions");

        // ========== ЛАЙКИ И ДИЗЛАЙКИ ==========

        /// <summary>
        /// Инициализация таблицы реакций
        /// </summary>
        public static void InitializeReactionsTable()
        {
            using var db = new SqliteConnection(ConnectionString);

            // Создаем таблицу реакций
            db.Execute(@"
                CREATE TABLE IF NOT EXISTS MessageReactions (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    MessageId INTEGER NOT NULL,
                    ChaterId TEXT NOT NULL,
                    Reaction TEXT NOT NULL CHECK(Reaction IN ('like', 'dislike')),
                    CreatedAt INTEGER NOT NULL,
                    UNIQUE(MessageId, ChaterId)
                )");

            // Добавляем колонки в ChatLogs если их нет
            try
            {
                db.Execute("ALTER TABLE ChatLogs ADD COLUMN Likes INTEGER DEFAULT 0");
                Debug.WriteLine("[DB] Колонка Likes добавлена в ChatLogs");
            }
            catch { /* колонка уже существует */ }

            try
            {
                db.Execute("ALTER TABLE ChatLogs ADD COLUMN Dislikes INTEGER DEFAULT 0");
                Debug.WriteLine("[DB] Колонка Dislikes добавлена в ChatLogs");
            }
            catch { /* колонка уже существует */ }

            // Добавляем колонку MessageNumber если ее нет
            try
            {
                db.Execute("ALTER TABLE ChatLogs ADD COLUMN MessageNumber INTEGER DEFAULT 0");
                Debug.WriteLine("[DB] Колонка MessageNumber добавлена в ChatLogs");
            }
            catch { /* колонка уже существует */ }

            Debug.WriteLine("[DB] Таблица MessageReactions инициализирована");
        }

        /// <summary>
        /// Получение сообщений стрима с реакциями пользователя
        /// </summary>
        public static List<ChatLogMessage> GetChatLogsWithReactions(string sessionId, string currentChaterId)
        {
            using var db = new SqliteConnection(ConnectionString);

            string sql = @"
                SELECT 
                    c.Id, c.SessionId, c.ChaterId, c.Message, c.Timestamp, c.MessageNumber,
                    COALESCE(c.Likes, 0) as Likes,
                    COALESCE(c.Dislikes, 0) as Dislikes,
                    r.Reaction as UserReaction
                FROM ChatLogs c
                LEFT JOIN MessageReactions r ON c.Id = r.MessageId AND r.ChaterId = @currentChaterId
                WHERE c.SessionId = @sessionId
                ORDER BY c.MessageNumber ASC";

            return db.Query<ChatLogMessage>(sql, new { sessionId, currentChaterId }).ToList();
        }

        /// <summary>
        /// Поставить лайк сообщению
        /// </summary>
        public static void LikeMessage(long messageId, string chaterId)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Проверяем существующую реакцию
                var existing = db.QuerySingleOrDefault<string>(
                    "SELECT Reaction FROM MessageReactions WHERE MessageId = @messageId AND ChaterId = @chaterId",
                    new { messageId, chaterId });

                if (existing == null)
                {
                    // Новый лайк
                    db.Execute(@"
                        INSERT INTO MessageReactions (MessageId, ChaterId, Reaction, CreatedAt)
                        VALUES (@messageId, @chaterId, 'like', @now)",
                        new { messageId, chaterId, now });

                    db.Execute(@"
                        UPDATE ChatLogs SET Likes = Likes + 1 
                        WHERE Id = @messageId", new { messageId });

                    Debug.WriteLine($"[DB] Лайк поставлен на сообщение {messageId}");
                }
                else if (existing == "dislike")
                {
                    // Меняем дизлайк на лайк
                    db.Execute(@"
                        UPDATE MessageReactions SET Reaction = 'like', CreatedAt = @now
                        WHERE MessageId = @messageId AND ChaterId = @chaterId",
                        new { messageId, chaterId, now });

                    db.Execute(@"
                        UPDATE ChatLogs SET Likes = Likes + 1, Dislikes = Dislikes - 1 
                        WHERE Id = @messageId", new { messageId });

                    Debug.WriteLine($"[DB] Дизлайк на {messageId} изменен на лайк");
                }
                // Если уже лайк - ничего не делаем

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine($"[DB ERROR] LikeMessage: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Поставить дизлайк сообщению
        /// </summary>
        public static void DislikeMessage(long messageId, string chaterId)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

                // Проверяем существующую реакцию
                var existing = db.QuerySingleOrDefault<string>(
                    "SELECT Reaction FROM MessageReactions WHERE MessageId = @messageId AND ChaterId = @chaterId",
                    new { messageId, chaterId });

                if (existing == null)
                {
                    // Новый дизлайк
                    db.Execute(@"
                        INSERT INTO MessageReactions (MessageId, ChaterId, Reaction, CreatedAt)
                        VALUES (@messageId, @chaterId, 'dislike', @now)",
                        new { messageId, chaterId, now });

                    db.Execute(@"
                        UPDATE ChatLogs SET Dislikes = Dislikes + 1 
                        WHERE Id = @messageId", new { messageId });

                    Debug.WriteLine($"[DB] Дизлайк поставлен на сообщение {messageId}");
                }
                else if (existing == "like")
                {
                    // Меняем лайк на дизлайк
                    db.Execute(@"
                        UPDATE MessageReactions SET Reaction = 'dislike', CreatedAt = @now
                        WHERE MessageId = @messageId AND ChaterId = @chaterId",
                        new { messageId, chaterId, now });

                    db.Execute(@"
                        UPDATE ChatLogs SET Likes = Likes - 1, Dislikes = Dislikes + 1 
                        WHERE Id = @messageId", new { messageId });

                    Debug.WriteLine($"[DB] Лайк на {messageId} изменен на дизлайк");
                }
                // Если уже дизлайк - ничего не делаем

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine($"[DB ERROR] DislikeMessage: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Удалить реакцию с сообщения
        /// </summary>
        public static void RemoveReaction(long messageId, string chaterId)
        {
            using var db = new SqliteConnection(ConnectionString);
            db.Open();
            using var tx = db.BeginTransaction();

            try
            {
                // Получаем текущую реакцию
                var existing = db.QuerySingleOrDefault<string>(
                    "SELECT Reaction FROM MessageReactions WHERE MessageId = @messageId AND ChaterId = @chaterId",
                    new { messageId, chaterId });

                if (existing != null)
                {
                    // Удаляем реакцию
                    db.Execute(@"
                        DELETE FROM MessageReactions 
                        WHERE MessageId = @messageId AND ChaterId = @chaterId",
                        new { messageId, chaterId });

                    // Обновляем счетчики
                    if (existing == "like")
                    {
                        db.Execute(@"
                            UPDATE ChatLogs SET Likes = Likes - 1 
                            WHERE Id = @messageId", new { messageId });
                    }
                    else if (existing == "dislike")
                    {
                        db.Execute(@"
                            UPDATE ChatLogs SET Dislikes = Dislikes - 1 
                            WHERE Id = @messageId", new { messageId });
                    }

                    Debug.WriteLine($"[DB] Реакция удалена с сообщения {messageId}");
                }

                tx.Commit();
            }
            catch (Exception ex)
            {
                tx.Rollback();
                Debug.WriteLine($"[DB ERROR] RemoveReaction: {ex.Message}");
                throw;
            }
        }

        // --- SQL КОНСТАНТЫ ---
        public static class Sql
        {
            public const string InitTables = @"
    CREATE TABLE IF NOT EXISTS Chaters (
        Id TEXT PRIMARY KEY, 
        PersonId TEXT, 
        MainName TEXT NOT NULL, 
        DisplayName TEXT,
        AvatarFileName TEXT DEFAULT 'default.png', 
        Rank INTEGER DEFAULT 0, 
        KarmaMultiplier REAL DEFAULT 1.0, 
        Karma REAL DEFAULT 0.0,
        TotalKarma REAL DEFAULT 0.0,
        MessageCount INTEGER DEFAULT 0, 
        FirstSeen INTEGER, 
        LastMessageTime INTEGER, 
        MessageXamlTemplate TEXT,
        CustomKarmaKey INTEGER, 
        IsKeyPermanent INTEGER DEFAULT 0
    );
    
    CREATE TABLE IF NOT EXISTS ExternalAccounts (
        ExternalId TEXT PRIMARY KEY, 
        ChaterId TEXT NOT NULL, 
        Platform TEXT NOT NULL, 
        OriginalName TEXT, 
        FOREIGN KEY(ChaterId) REFERENCES Chaters(Id) ON DELETE CASCADE
    );
    
    CREATE TABLE IF NOT EXISTS StreamSessions (
        Id TEXT PRIMARY KEY, 
        Number INTEGER, 
        Title TEXT, 
        StartTime INTEGER, 
        EndTime INTEGER
    );
    
    CREATE TABLE IF NOT EXISTS ChatLogs (
        Id INTEGER PRIMARY KEY AUTOINCREMENT, 
        SessionId TEXT, 
        ChaterId TEXT, 
        Message TEXT, 
        Timestamp INTEGER,
        MessageNumber INTEGER DEFAULT 0,
        Likes INTEGER DEFAULT 0,
        Dislikes INTEGER DEFAULT 0
    );";

            public const string LoadBase = @"
    SELECT 
        c.Id, c.PersonId, c.MainName as Login, 
        COALESCE(c.DisplayName, '') as DisplayName, 
        c.AvatarFileName, c.Rank, c.Karma, c.TotalKarma, 
        c.MessageCount, c.FirstSeen, c.LastMessageTime, 
        c.MessageXamlTemplate,
        COALESCE(c.CustomKarmaKey, c.rowid) as KarmaKey, 
        c.IsKeyPermanent as IsKarmaKeyPermanent,
        e.ExternalId, e.Platform, e.OriginalName, e.ChaterId
    FROM Chaters c 
    LEFT JOIN ExternalAccounts e ON c.Id = e.ChaterId";

            public const string LoadByExtId = LoadBase + " WHERE e.ExternalId = @extId";
            public const string LoadByKarmaKey = LoadBase + " WHERE KarmaKey = @k";
            public const string LoadAll = LoadBase;

            public const string GetLogsBySession = @"
        SELECT l.Timestamp, l.Message, c.MainName as Author 
        FROM ChatLogs l 
        JOIN Chaters c ON l.ChaterId = c.Id 
        WHERE l.SessionId = @sid 
        ORDER BY l.Timestamp ASC";

            public const string SaveChaterFull = @"
        INSERT INTO Chaters (
            Id, PersonId, MainName, DisplayName, AvatarFileName, Rank, 
            KarmaMultiplier, Karma, TotalKarma, MessageCount, FirstSeen, 
            LastMessageTime, MessageXamlTemplate, CustomKarmaKey, IsKeyPermanent
        ) VALUES (
            @Id, @PersonId, @Name, @DisplayName, @AvatarFileName, @Rank, 
            @KarmaMultiplier, @Karma, @TotalKarma, @MessageCount, @FirstSeen, 
            @LastMessageTime, @MessageXamlTemplate, @CustomKarmaKey, @IsKeyPermanent
        ) 
        ON CONFLICT(Id) DO UPDATE SET 
            DisplayName = excluded.DisplayName, 
            MainName = excluded.MainName,
            AvatarFileName = excluded.AvatarFileName,
            Rank = excluded.Rank,
            Karma = excluded.Karma,
            TotalKarma = excluded.TotalKarma,
            MessageCount = excluded.MessageCount,
            LastMessageTime = excluded.LastMessageTime,
            MessageXamlTemplate = excluded.MessageXamlTemplate,
            CustomKarmaKey = excluded.CustomKarmaKey,
            IsKeyPermanent = excluded.IsKeyPermanent";

            public const string UpdateChaterStats = @"
        UPDATE Chaters SET 
            Karma = @Karma, 
            TotalKarma = @TotalKarma, 
            MessageCount = @MessageCount, 
            LastMessageTime = @LastMessageTime, 
            Rank = @Rank
        WHERE Id = @Id";

            public const string SaveExternal = @"
        INSERT INTO ExternalAccounts (ExternalId, ChaterId, Platform, OriginalName)
        VALUES (@ExternalId, @ChaterId, @Platform, @Name)
        ON CONFLICT(ExternalId) DO UPDATE SET OriginalName = excluded.OriginalName";

            public const string SaveSession = @"
        INSERT INTO StreamSessions (Id, Number, Title, StartTime, EndTime) 
        VALUES (@Id, @Number, @Title, @StartTime, @EndTime) 
        ON CONFLICT(Id) DO UPDATE SET Title=excluded.Title, EndTime=excluded.EndTime";

            public const string SaveLog = @"
        INSERT INTO ChatLogs (SessionId, ChaterId, Message, Timestamp) 
        VALUES (@SessionId, @ChaterId, @Message, @Timestamp)";

            public const string GetActiveSession = @"
        SELECT * FROM StreamSessions 
        WHERE EndTime = 0 OR EndTime IS NULL 
        ORDER BY StartTime DESC LIMIT 1";
        }

        public static StreamSession? GetSessionByNumber(int number)
        {
            using var db = new SqliteConnection(ConnectionString);
            return db.QueryFirstOrDefault<StreamSession>(
                "SELECT * FROM StreamSessions WHERE Number = @number ORDER BY StartTime DESC LIMIT 1",
                new { number });
        }
    }
}