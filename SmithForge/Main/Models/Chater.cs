using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Collections;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SmithForge.Main.Models
{
    public partial class Chater : ObservableObject
    {
        // --- ИДЕНТИФИКАЦИЯ (Identity) ---
        [ObservableProperty] private string _id = Guid.NewGuid().ToString();
        [ObservableProperty] private string _personId = string.Empty;
        public ObservableCollection<ExternalAccount> Accounts { get; set; } = new();

        // --- ИМЕНА И ОТОБРАЖЕНИЕ (Profile) ---
        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EffectiveName))]
        private string _login = string.Empty;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(EffectiveName))]
        private string _displayName = string.Empty;

        [ObservableProperty] private bool _isRecentlySaved;

        public string EffectiveName => !string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Login;

        partial void OnDisplayNameChanging(string value)
        {
            if (string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(_displayName))
                return;
        }

        // --- ЭКОНОМИКА И ПРОГРЕСС (Progression) ---
        [ObservableProperty] private int _rank; // Текущий ранг (меняется от любых событий)
        [ObservableProperty] private int _karmaKey;
        [ObservableProperty] private bool _isKarmaKeyPermanent;
        [ObservableProperty] private double _karma;
        [ObservableProperty] private double _totalKarma;

        // --- АУРА (репутация) - ПЕРЕНЕСЕНО В CHATER ---
        [ObservableProperty] private int _aura; // Общая аура (может быть отрицательной)
        [ObservableProperty] private int _auraWeek; // Аура за последние 7 дней
        [ObservableProperty] private int _auraMonth; // Аура за последние 30 дней

        public string KarmaKeyDisplay => $"#{KarmaKey}";
        public string KarmaDisplay => Karma.ToString("F1");
        public string TotalKarmaDisplay => TotalKarma.ToString("F1");
        public double KarmaMultiplier => 1.0 + (Rank * 0.1);

        // --- СТАТИСТИКА И ВРЕМЯ (Activity) ---
        [ObservableProperty] private long _messageCount;
        [ObservableProperty] private long _lastMessageTime;
        [ObservableProperty] private long _firstSeen = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // --- ВИЗУАЛЬНАЯ КАСТОМИЗАЦИЯ (Customization) ---
        [ObservableProperty] private string _avatarFileName = "default.png";
        [ObservableProperty] private string _messageXamlTemplate = string.Empty;

        public ConcurrentHashSet<string> BadgeIcons { get; } = new();
        public ConcurrentHashSet<string> Channels { get; } = new();

        // --- UI ХЕЛПЕРЫ (Computed Properties) ---
        public string FullAvatarPath => GetAvatarPath();

        private string GetAvatarPath()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars");

            // 1. Custom
            string customPath = Path.Combine(basePath, "custom", $"{Id}.png");
            Debug.WriteLine($"[Chater] Проверка custom: {customPath}, exists={File.Exists(customPath)}");
            if (File.Exists(customPath))
                return customPath;

            // 2. Platform
            string platformPath = Path.Combine(basePath, "platform", $"{Id}.png");
            Debug.WriteLine($"[Chater] Проверка platform: {platformPath}, exists={File.Exists(platformPath)}");
            if (File.Exists(platformPath))
                return platformPath;

            // 3. Default по рангу
            string rankPath = Path.Combine(basePath, "default", $"rank{Rank}.png");
            Debug.WriteLine($"[Chater] Проверка rank: {rankPath}, exists={File.Exists(rankPath)}");
            if (File.Exists(rankPath))
                return rankPath;

            // 4. Unknown
            string unknownPath = Path.Combine(basePath, "default", "unknown.png");
            Debug.WriteLine($"[Chater] Проверка unknown: {unknownPath}, exists={File.Exists(unknownPath)}");
            return File.Exists(unknownPath) ? unknownPath : string.Empty;
        }

        // Метод для принудительного обновления аватара в UI
        public void RefreshAvatar()
        {
            Debug.WriteLine($"[Chater] RefreshAvatar вызван для {Id}, FullAvatarPath={FullAvatarPath}");
            OnPropertyChanged(nameof(FullAvatarPath));
        }

        // Метод для проверки существования аватара
        public bool HasCustomAvatar()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars");
            string customPath = Path.Combine(basePath, "custom", $"{Id}.png");
            return File.Exists(customPath);
        }

        public bool HasPlatformAvatar()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars");
            string platformPath = Path.Combine(basePath, "platform", $"{Id}.png");
            return File.Exists(platformPath);
        }

        // Метод для получения информации об аватаре (для отладки)
        public string GetAvatarDebugInfo()
        {
            string basePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Avatars");
            string customPath = Path.Combine(basePath, "custom", $"{Id}.png");
            string platformPath = Path.Combine(basePath, "platform", $"{Id}.png");
            string rankPath = Path.Combine(basePath, "default", $"rank{Rank}.png");
            string unknownPath = Path.Combine(basePath, "default", "unknown.png");

            return $"Custom: {(File.Exists(customPath) ? "✅" : "❌")} {customPath}\n" +
                   $"Platform: {(File.Exists(platformPath) ? "✅" : "❌")} {platformPath}\n" +
                   $"Rank {Rank}: {(File.Exists(rankPath) ? "✅" : "❌")} {rankPath}\n" +
                   $"Unknown: {(File.Exists(unknownPath) ? "✅" : "❌")} {unknownPath}\n" +
                   $"Final: {FullAvatarPath}";
        }

        private string GetPrimaryPlatform()
        {
            var account = Accounts.FirstOrDefault();
            if (account == null) return "";

            return account.Platform?.ToLower() switch
            {
                "tw" or "twitch" => "twitch",
                "yt" or "youtube" => "youtube",
                "gg" or "goodgame" => "goodgame",
                _ => ""
            };
        }
        public string AllPlatforms => string.Join(", ", Accounts.Select(a => a.DisplayName));

        // --- СВОЙСТВА АУРЫ ---
        public string AuraStatus => Aura switch
        {
            >= 100 => "🌟 Легенда",
            >= 50 => "✨ Герой",
            >= 20 => "⭐ Положительный",
            >= 0 => "😐 Нейтральный",
            >= -20 => "☁️ Туманный",
            >= -50 => "🌧️ Отрицательный",
            _ => "⚡ Тёмный"
        };

        public string AuraColor => Aura switch
        {
            >= 100 => "#FFD700", // Золото
            >= 50 => "#C0C0C0",  // Серебро
            >= 20 => "#CD7F32",  // Бронза
            >= 0 => "#5C6BC0",   // Синий
            >= -20 => "#9C27B0", // Фиолетовый
            >= -50 => "#FF5722", // Оранжевый
            _ => "#F44336"       // Красный
        };
    }

    public partial class ExternalAccount : ObservableObject
    {
        [ObservableProperty] private string _externalId = string.Empty;
        [ObservableProperty] private string _platform = string.Empty;
        [ObservableProperty] private string _originalName = string.Empty;

        public string DisplayName => $"{PlatformFullName}: {OriginalName}";

        public string PlatformShort => Platform.ToLower() switch
        {
            "tw" or "twitch" => "TW",
            "yt" or "youtube" => "YT",
            "gg" or "goodgame" => "GG",
            _ => Platform.Length > 2 ? Platform[..2].ToUpper() : Platform.ToUpper()
        };

        public string PlatformFullName => Platform.ToLower() switch
        {
            "tw" or "twitch" => "Twitch",
            "yt" or "youtube" => "YouTube",
            "gg" or "goodgame" => "GoodGame",
            _ => Platform
        };

        public string PlatformColor => Platform.ToLower() switch
        {
            "tw" or "twitch" => "#b863a7",
            "yt" or "youtube" => "#e85873",
            "gg" or "goodgame" => "#7392d8",
            _ => "#666666"
        };
    }
}