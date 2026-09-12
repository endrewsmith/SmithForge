using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System.Collections.ObjectModel;
using System.Windows;
using System.Text.RegularExpressions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace SmithForge.Features.StickersOverlay
{
    public partial class StickersOverlayViewModel : ObservableObject
    {
        [ObservableProperty]
        private int _stickerDisplayTimeMs = 5000;
        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private bool _isEnabled = true;

        [ObservableProperty] private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        // ОЧЕРЕДЬ СТИКЕРОВ
        private readonly Queue<DisplayMessageViewModel> _stickerQueue = new();
        private bool _isShowingSticker = false;
        private readonly object _queueLock = new object();

        public StickersOverlayViewModel()
        {
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
        }

        public void SetDisplayTime(int milliseconds)
        {
            StickerDisplayTimeMs = milliseconds;
        }

        public void SetMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            foreach (var msg in DisplayMessages)
            {
                ApplyModeSettings(msg);
            }
        }

        private void ApplyModeSettings(DisplayMessageViewModel msgVm)
        {
            switch (_currentMode)
            {
                case ChatDisplayMode.Compact:
                    msgVm.ShowAvatar = false;
                    msgVm.ShowRank = false;
                    break;
                default:
                    msgVm.ShowAvatar = true;
                    msgVm.ShowRank = true;
                    break;
            }
        }

        public void ShowSticker(Chater user, CommonMessage msg)
        {
            if (!IsEnabled) return;

            // ✅ УБЕЖДАЕМСЯ, ЧТО МЫ В UI ПОТОКЕ
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowSticker(user, msg));
                return;
            }

            try
            {
                // Извлекаем путь к стикеру из тега
                var stickerMatch = Regex.Match(msg.Message, @"<sticker pack='(\d+)' id='(\d+)' path='([^']+)'");
                string stickerPath = null;
                string textContent = "";

                if (stickerMatch.Success)
                {
                    stickerPath = stickerMatch.Groups[3].Value;
                    // Удаляем тег стикера, но сохраняем остальной текст
                    textContent = Regex.Replace(msg.Message, @"<sticker[^>]*/>", "").Trim();
                }
                else
                {
                    // Если нет тега стикера, просто очищаем все теги
                    textContent = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                }

                // Если нет стикера — выходим (стикер обязателен)
                if (string.IsNullOrEmpty(stickerPath)) return;

                // Создаем ViewModel для стикера
                var msgVm = new DisplayMessageViewModel(user, msg, stickerPath);
                msgVm.MessageText = textContent;

                System.Diagnostics.Debug.WriteLine($"[Stickers] Стикер: path={stickerPath}, текст='{textContent}'");

                ApplyModeSettings(msgVm);

                lock (_queueLock)
                {
                    _stickerQueue.Enqueue(msgVm);
                    System.Diagnostics.Debug.WriteLine($"[StickersQueue] Добавлен стикер, текст: '{textContent}', очередь: {_stickerQueue.Count}");
                }

                if (!_isShowingSticker)
                {
                    ProcessQueue();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StickersOverlay ShowSticker Error] {ex.Message}");
            }
        }

        private async void ProcessQueue()
        {
            if (_isShowingSticker) return;

            lock (_queueLock)
            {
                if (_stickerQueue.Count == 0) return;
                _isShowingSticker = true;
            }

            while (true)
            {
                DisplayMessageViewModel? nextSticker = null;

                lock (_queueLock)
                {
                    if (_stickerQueue.Count > 0)
                    {
                        nextSticker = _stickerQueue.Dequeue();
                        System.Diagnostics.Debug.WriteLine($"[StickersQueue] Показываем стикер, осталось: {_stickerQueue.Count}");
                    }
                }

                if (nextSticker == null) break;

                // ВОСПРОИЗВЕСТИ ЗВУК СТИКЕРА
                VoiceService.PlayStickerSound();

                // Показываем стикер
                await ShowStickerInternal(nextSticker);

                // Ждем, пока стикер висит на экране
                await Task.Delay(StickerDisplayTimeMs);

                // Убираем стикер
                await HideStickerInternal(nextSticker);

                // Небольшая задержка между стикерами
                await Task.Delay(300);
            }

            lock (_queueLock)
            {
                _isShowingSticker = false;
            }
        }

        private Task ShowStickerInternal(DisplayMessageViewModel stickerVm)
        {
            var tcs = new TaskCompletionSource<bool>();

            // ✅ УБЕЖДАЕМСЯ, ЧТО МЫ В UI ПОТОКЕ
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ShowStickerInternal(stickerVm));
                return tcs.Task;
            }

            try
            {
                // Очищаем предыдущие стикеры
                DisplayMessages.Clear();
                // Добавляем новый
                DisplayMessages.Add(stickerVm);
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ShowStickerInternal Error] {ex.Message}");
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        private Task HideStickerInternal(DisplayMessageViewModel stickerVm)
        {
            var tcs = new TaskCompletionSource<bool>();

            // ✅ УБЕЖДАЕМСЯ, ЧТО МЫ В UI ПОТОКЕ
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => HideStickerInternal(stickerVm));
                return tcs.Task;
            }

            try
            {
                if (DisplayMessages.Contains(stickerVm))
                {
                    DisplayMessages.Remove(stickerVm);
                }
                tcs.SetResult(true);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[HideStickerInternal Error] {ex.Message}");
                tcs.SetException(ex);
            }

            return tcs.Task;
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            // ✅ УБЕЖДАЕМСЯ, ЧТО МЫ В UI ПОТОКЕ
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => OnChaterUpdated(updatedChater));
                return;
            }

            try
            {
                // ✅ БЕРЁМ КОПИЮ СПИСКА, ЧТОБЫ ИЗБЕЖАТЬ КОНФЛИКТОВ
                var messagesToUpdate = DisplayMessages
                    .Where(m => m.User?.Id == updatedChater.Id)
                    .ToList();

                foreach (var msg in messagesToUpdate)
                {
                    msg.User = updatedChater;
                    msg.UpdateMessageCount();
                    msg.RefreshAvatar();
                }

                if (messagesToUpdate.Any())
                {
                    System.Diagnostics.Debug.WriteLine($"[StickersOverlay] Обновлено {messagesToUpdate.Count} сообщений для {updatedChater.EffectiveName}");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[StickersOverlay] Ошибка обновления: {ex.Message}");
            }
        }

        public void ClearStickers()
        {
            // ✅ УБЕЖДАЕМСЯ, ЧТО МЫ В UI ПОТОКЕ
            if (!Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.Invoke(() => ClearStickers());
                return;
            }

            DisplayMessages.Clear();
            lock (_queueLock)
            {
                _stickerQueue.Clear();
            }
        }

        public void Dispose()
        {
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        }
    }
}