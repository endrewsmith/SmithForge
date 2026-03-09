using SmithForge.Main.Models;

public class CommonMessage
{
    public string Type { get; set; } = string.Empty;
    public string Login { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int Priority { get; set; }
    public long Timestamp { get; set; }
    public string KarmaKeyDisplay { get; set; } = string.Empty;

    public int MessageNumber { get; set; }  // <-- ДОБАВЛЕНО

    public Chater? User { get; set; }
    public string TypeLogin => $"{Type}-{Login}";

    public MessageLength LengthCategory
    {
        get
        {
            int len = Message?.Length ?? 0;
            if (len <= 20) return MessageLength.Short;
            if (len <= 100) return MessageLength.Medium;
            return MessageLength.Long;
        }
    }

    public int DisplayTimeMs => LengthCategory switch
    {
        MessageLength.Short => 7000,   // 3 секунды
        MessageLength.Medium => 20000,  // 5 секунд
        MessageLength.Long => 30000,    // 8 секунд
        _ => 5000
    };
}