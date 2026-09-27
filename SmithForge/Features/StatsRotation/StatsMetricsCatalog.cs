namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Каталог всех метрик, построенных на маркерах в сообщениях.
    /// Добавить новый топ = добавить строчку сюда.
    /// </summary>
    public static class StatsMetricsCatalog
    {
        public static readonly StatsMetric[] All = new[]
        {
            // ═══════════════════════════════════════════════════════
            // СТИКЕРЫ  (пишутся как <sticker .../>)
            // ═══════════════════════════════════════════════════════
            new StatsMetric(
                "top_stickers_stream",
                "🎨 Топ-3 по стикерам (за стрим)",
                "<sticker", "стикеров",
                StatsScope.ThisStream, 25, 15),

            new StatsMetric(
                "top_stickers_all",
                "🎨 Топ-3 по стикерам (за всё время)",
                "<sticker", "стикеров",
                StatsScope.AllTime, 26, 15),

            // ═══════════════════════════════════════════════════════
            // ГОЛОСОВЫЕ  (пишутся как <voice>...</voice>)
            // ═══════════════════════════════════════════════════════
            new StatsMetric(
                "top_voice_stream",
                "🎤 Топ-3 по озвучкам (за стрим)",
                "<voice>", "озвучек",
                StatsScope.ThisStream, 27, 15),

            new StatsMetric(
                "top_voice_all",
                "🎤 Топ-3 по озвучкам (за всё время)",
                "<voice>", "озвучек",
                StatsScope.AllTime, 28, 15),

            // ═══════════════════════════════════════════════════════
            // ЗВУКИ  (пишутся как <sound .../>)
            // ═══════════════════════════════════════════════════════
            new StatsMetric(
                "top_sounds_stream",
                "🔊 Топ-3 по звукам (за стрим)",
                "<sound", "звуков",
                StatsScope.ThisStream, 29, 15),

            new StatsMetric(
                "top_sounds_all",
                "🔊 Топ-3 по звукам (за всё время)",
                "<sound", "звуков",
                StatsScope.AllTime, 30, 15),

            // ═══════════════════════════════════════════════════════
            // ИНФО  (пишется как <cmd:info/> после правки MessageProcessor)
            // ═══════════════════════════════════════════════════════
            new StatsMetric(
                "top_info_stream",
                "📖 Топ-3 читателей справки (за стрим)",
                "<cmd:info/>", "просмотров",
                StatsScope.ThisStream, 31, 15),

            new StatsMetric(
                "top_info_all",
                "📖 Топ-3 читателей справки (за всё время)",
                "<cmd:info/>", "просмотров",
                StatsScope.AllTime, 32, 15),

            // ═══════════════════════════════════════════════════════
            // ЛАЙКИ / ДИЗЛАЙКИ
            // ═══════════════════════════════════════════════════════
            new StatsMetric(
                "top_likes_all",
                "👍 Топ-3 по лайкам (за всё время)",
                "<cmd:like/>", "лайков",
                StatsScope.AllTime, 33, 15),

            new StatsMetric(
                "top_dislikes_all",
                "👎 Топ-3 по дизлайкам (за всё время)",
                "<cmd:dislike/>", "дизлайков",
                StatsScope.AllTime, 34, 15),
        };
    }
}