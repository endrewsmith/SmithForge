namespace SmithForge.Features.StatsRotation
{
    /// <summary>
    /// Период, за который считается метрика.
    /// </summary>
    public enum StatsScope
    {
        ThisStream,
        AllTime,
        Last24h,
        Last7d,
        ThisMonth
    }
}