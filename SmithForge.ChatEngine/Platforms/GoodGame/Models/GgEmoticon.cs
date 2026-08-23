namespace SmithForge.ChatEngine.Platforms.GoodGame.Models
{
    public class GgEmoticon
    {
        public long Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool IsAnimated { get; set; }
        public override string ToString() => $":{Code}:";
    }
}