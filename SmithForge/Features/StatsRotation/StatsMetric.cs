namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Описание одной метрики — правило ротации, работающее по маркеру в сообщениях.
    /// </summary>
    public sealed record StatsMetric(
        string Key,
        string Title,
        string Marker,
        string ValueSuffix,
        StatsScope Scope,
        int Order,
        int DurationSeconds
    );
}