using SmithForge.Main.Models;

public class CommonMessage
{
    public string Type { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Priority { get; set; }
    public long Timestamp { get; set; }
    public string KarmaKeyDisplay { get; set; } = string.Empty;

    public int MessageNumber { get; set; }

    public Chater? User { get; set; }
    public string TypeLogin => $"{Type}-{Login}";

    public bool IsProcessedByCommand { get; set; }

    // Добавляем поле для хранения базового времени
    private int _baseDisplayTimeMs;

    public MessageLength LengthCategory
    {
        get
        {
            int len = Message?.Length ?? 0;

            // ОТЛАДКА
            System.Diagnostics.Debug.WriteLine($"[DEBUG] Message: '{Message}', Length: {len}, Bytes: {System.Text.Encoding.UTF8.GetByteCount(Message ?? "")}");

            if (len <= 20) return MessageLength.Short;
            if (len <= 100) return MessageLength.Medium;
            return MessageLength.Long;
        }
    }

    // Базовое время (вычисляется на лету)
    public int BaseDisplayTimeMs => LengthCategory switch
    {
        MessageLength.Short => 7000,
        MessageLength.Medium => 20000,
        MessageLength.Long => 30000,
        _ => 5000
    };

    private int? _customDisplayTimeMs;

    // Итоговое время отображения
    public int DisplayTimeMs
    {
        get => _customDisplayTimeMs ?? BaseDisplayTimeMs;
        set => _customDisplayTimeMs = value;
    }

    public bool ShouldChargeForCommand { get; set; } = true;
}