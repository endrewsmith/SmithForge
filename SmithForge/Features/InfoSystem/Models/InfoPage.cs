using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmithForge.Features.InfoSystem.Models
{
    // InfoSystem/Models/InfoPage.cs
    public class InfoPage
    {
        public string Id { get; set; } = string.Empty;          // "help", "karma"
        public string Title { get; set; } = string.Empty;        // "📚 Справочник"
        public string? ParentId { get; set; }                   // для навигации "назад"
        public string Content { get; set; } = string.Empty;      // HTML-содержимое
        public List<PageLink> Links { get; set; } = new();      // ссылки на другие страницы
        public string Icon { get; set; } = "📄";
        public DateTime LoadedAt { get; set; }
        public bool IsCached { get; set; }
    }

    public class PageLink
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
