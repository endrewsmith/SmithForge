using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SmithForge.Features.InfoSystem;
using System;
using System.Diagnostics;

namespace SmithForge.Main.Services
{
    /// <summary>
    /// Координатор генерации страниц стикеров и звуков.
    /// </summary>
    public partial class StickersCoordinator : ObservableObject
    {
        private readonly StickerPageService _stickerPageService;
        private readonly SoundPageService _soundPageService;

        // ============================================================
        // СТАТУСЫ ГЕНЕРАЦИИ
        // ============================================================
        [ObservableProperty]
        private string _stickerPagesStatus = "Готово";

        [ObservableProperty]
        private string _soundPagesStatus = "Готово";

        // ============================================================
        // КОНСТРУКТОР
        // ============================================================
        public StickersCoordinator(
            StickerPageService stickerPageService,
            SoundPageService soundPageService)
        {
            _stickerPageService = stickerPageService ?? throw new ArgumentNullException(nameof(stickerPageService));
            _soundPageService = soundPageService ?? throw new ArgumentNullException(nameof(soundPageService));
        }

        // ============================================================
        // КОМАНДЫ
        // ============================================================
        [RelayCommand]
        private void GenerateStickerPages()
        {
            try
            {
                StickerPagesStatus = "⏳ Сканирование папок...";

                var count = _stickerPageService.GenerateAllPages();

                if (count > 0)
                {
                    StickerPagesStatus = $"✅ Сгенерировано {count} страниц стикеров";
                    Debug.WriteLine($"[StickerPages] Сгенерировано {count} страниц");
                }
                else
                {
                    StickerPagesStatus = "❌ Нет паков со стикерами";
                }
            }
            catch (Exception ex)
            {
                StickerPagesStatus = $"❌ Ошибка: {ex.Message}";
                Debug.WriteLine($"[StickerPages] Ошибка: {ex.Message}");
            }
        }

        [RelayCommand]
        private void GenerateSoundPages()
        {
            try
            {
                SoundPagesStatus = "⏳ Сканирование звуков...";
                var count = _soundPageService.GenerateAllPages();
                SoundPagesStatus = count > 0 ? $"✅ Сгенерировано {count} страниц звуков" : "❌ Нет паков со звуками";
            }
            catch (Exception ex)
            {
                SoundPagesStatus = $"❌ Ошибка: {ex.Message}";
            }
        }
    }
}