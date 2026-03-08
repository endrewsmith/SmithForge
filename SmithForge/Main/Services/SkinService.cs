using SmithForge.Main.Models;
using System;
using System.IO;
using System.Linq;

namespace SmithForge.Main.Services
{
    public static class SkinService
    {
        private static string SkinsBaseDir => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SF_Data", "Assets", "Skins");
        private static string RanksDir => Path.Combine(SkinsBaseDir, "Ranks");
        private static string DefaultDir => Path.Combine(SkinsBaseDir, "Default");

        public static string GetSkinPath(Chater chater)
        {
            try
            {
                if (chater == null)
                    return GetDefaultPath();

                // Первые 10 сообщений - используем rank_0.xaml или дефолт
                if (chater.MessageCount <= 10)
                {
                    // Пробуем rank_0.xaml
                    string rank0Path = Path.Combine(RanksDir, "rank_0.xaml");
                    if (File.Exists(rank0Path))
                    {
                        System.Diagnostics.Debug.WriteLine($"[SkinService] Используем rank_0.xaml (первые 10 сообщений)");
                        return rank0Path;
                    }

                    // Если нет rank_0, пробуем дефолт
                    return GetDefaultPath();
                }

                // После 10 сообщений - ищем рабочий ранговый шаблон
                return FindWorkingRankPath(chater.Rank);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinService] Ошибка: {ex.Message}");
                return GetDefaultPath();
            }
        }

        private static string FindWorkingRankPath(int targetRank)
        {
            try
            {
                if (!Directory.Exists(RanksDir))
                    return GetDefaultPath();

                // Идем от целевого ранга вниз до 0
                for (int rank = targetRank; rank >= 0; rank--)
                {
                    string rankPath = Path.Combine(RanksDir, $"rank_{rank}.xaml");

                    if (File.Exists(rankPath))
                    {
                        // Проверяем, что файл не битый
                        if (IsValidRankFile(rankPath))
                        {
                            System.Diagnostics.Debug.WriteLine($"[SkinService] Выбран rank_{rank}.xaml для ранга {targetRank}");
                            return rankPath;
                        }
                        else
                        {
                            System.Diagnostics.Debug.WriteLine($"[SkinService] rank_{rank}.xaml битый, пробуем дальше");
                        }
                    }
                }

                System.Diagnostics.Debug.WriteLine($"[SkinService] Ранговые шаблоны не найдены, используем дефолт");
                return GetDefaultPath();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SkinService] Ошибка поиска: {ex.Message}");
                return GetDefaultPath();
            }
        }

        private static bool IsValidRankFile(string filePath)
        {
            try
            {
                string content = File.ReadAllText(filePath);

                // Проверяем наличие обязательных элементов
                bool hasClosingTag = content.Contains("</ResourceDictionary>");
                bool hasTemplate = content.Contains("ChatMessageTemplate");

                return hasClosingTag && hasTemplate;
            }
            catch
            {
                return false;
            }
        }

        private static string GetDefaultPath()
        {
            string defaultPath = Path.Combine(DefaultDir, "default.xaml");
            if (File.Exists(defaultPath))
            {
                return defaultPath;
            }

            System.Diagnostics.Debug.WriteLine($"[SkinService] default.xaml не найден, SkinLoader использует встроенный шаблон");
            return string.Empty;
        }
    }
}