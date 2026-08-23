using System.Text.RegularExpressions;
using SmithForge.ChatEngine.Platforms.GoodGame.Models;

namespace SmithForge.ChatEngine.Platforms.GoodGame
{
    public class HtmlUrlCleaner
    {
        private static readonly Regex HtmlUrlPattern =
            new Regex(@"<a target=""_blank"" rel=""nofollow"" href=""(.*?)"">\1</a>", RegexOptions.Compiled);

        public void Clean(GgMessage message)
        {
            if (string.IsNullOrEmpty(message.Text))
                return;

            var text = message.Text;
            var match = HtmlUrlPattern.Match(text);

            while (match.Success)
            {
                var url = match.Groups[1].Value;
                text = text.Replace(match.Value, url);
                match = HtmlUrlPattern.Match(text);
            }

            message.Text = text;
        }
    }
}