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
}