using SmithForge.Main.Models;
using SmithForge.Main.Services;
using SmithForge.ViewModels;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;

namespace SmithForge.Features.ImportantOverlay
{
    public class ImportantOverlayService
    {
        private ImportantOverlayWindow? _window;
        private ImportantOverlayViewModel? _viewModel;
        private bool _isInitialized = false;
        private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;
        public bool IsVisible => _window != null && _window.Visibility == Visibility.Visible;

        private bool _isHidden = false;
        private double _savedTop;
        private double _savedLeft;
        private readonly AppSettings _settings;

        // Очередь для ручного режима (хранит оригинальные объекты)
        private readonly Queue<(Chater chater, CommonMessage message, string text)> _manualQueue = new();
        private bool _isPlayingManual = false;

        public ImportantOverlayService(AppSettings settings)
        {
            _settings = settings;
        }

        public void Initialize(double top, double left, double width, double height, bool isSetupMode)
        {
            if (_isInitialized) return;
            _viewModel = new ImportantOverlayViewModel();
            _window = new ImportantOverlayWindow
            {
                DataContext = _viewModel,
                Top = top,
                Left = left,
                Width = width > 0 ? width : 450,
                Height = height > 0 ? height : 600,
                Visibility = Visibility.Visible
            };
            SetSetupMode(isSetupMode);
            SetDisplayMode(_currentMode);
            _isInitialized = true;
        }

        public void SetSetupMode(bool isSetupMode)
        {
            if (_window == null || _viewModel == null) return;
            _viewModel.IsSetupMode = isSetupMode;
            _window.SetClickThrough(!isSetupMode);
        }

        public void SetDisplayMode(ChatDisplayMode mode)
        {
            _currentMode = mode;
            _viewModel?.SetMode(mode);
        }

        public void SetHidden(bool isHidden)
        {
            if (_window == null) return;
            if (isHidden && !_isHidden)
            {
                _savedTop = _window.Top;
                _savedLeft = _window.Left;
                _window.Top = -2000;
                _window.Left = -2000;
                _isHidden = true;
            }
            else if (!isHidden && _isHidden)
            {
                _window.Top = _savedTop;
                _window.Left = _savedLeft;
                _isHidden = false;
            }
        }

        /// <summary>
        /// Главный метод показа сообщения
        /// </summary>
        public void ShowImportantMessage(Chater chater, CommonMessage message)
        {
            string importantText = $"Важное сообщение от {chater.EffectiveName}: {message.Message}";
            var settings = ConfigService.Load();

            Debug.WriteLine($"[ImportantOverlay] Режим: {(settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto ? "АВТО" : "РУЧНОЙ")}");
            Debug.WriteLine($"[ImportantOverlay] Текст: {importantText}");

            if (settings.ImportantPlaybackMode == ImportantPlaybackMode.Auto)
            {
                // АВТО-РЕЖИМ: сразу показываем и озвучиваем
                _ = Task.Run(async () => await ShowAndSpeakAsync(chater, message, importantText));
            }
            else
            {
                // РУЧНОЙ РЕЖИМ: сохраняем оригинальные объекты в очередь
                _manualQueue.Enqueue((chater, message, importantText));
                Debug.WriteLine($"[ManualQueue] Добавлено. Всего: {_manualQueue.Count}");

                // Обновляем счетчик
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
                    vm?.UpdateImportantQueueCount(_manualQueue.Count);
                });
            }
        }

        /// <summary>
        /// Показать и озвучить сообщение (для авто-режима и ручного воспроизведения)
        /// </summary>
        private async Task ShowAndSpeakAsync(Chater chater, CommonMessage message, string text)
        {
            try
            {
                Debug.WriteLine($"[ShowAndSpeak] Начинаем: {text}");

                // 1. Показываем сообщение в оверлее
                await Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    _viewModel?.ShowMessage(chater, message);
                });

                // Небольшая задержка для анимации
                await Task.Delay(200);

                // 2. Воспроизводим звук уведомления
                await VoiceService.PlayImportantSoundAsync();

                // 3. Воспроизводим голос
                await VoiceService.SayAsync(text);

                Debug.WriteLine("[ShowAndSpeak] Завершено успешно");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ShowAndSpeak Error] {ex.Message}");
                Debug.WriteLine($"[ShowAndSpeak Error] StackTrace: {ex.StackTrace}");
            }
        }

        /// <summary>
        /// Воспроизвести следующее сообщение из очереди (для ручного режима)
        /// </summary>
        public async Task PlayNextFromQueueAsync()
        {
            if (_isPlayingManual)
            {
                Debug.WriteLine("[ManualQueue] Уже воспроизводится");
                return;
            }

            if (_manualQueue.Count == 0)
            {
                Debug.WriteLine("[ManualQueue] Очередь пуста");
                return;
            }

            var (chater, message, text) = _manualQueue.Dequeue();
            Debug.WriteLine($"[ManualQueue] Воспроизводим: {text}, осталось: {_manualQueue.Count}");

            // Обновляем счетчик
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
                vm?.UpdateImportantQueueCount(_manualQueue.Count);
            });

            _isPlayingManual = true;

            try
            {
                // Используем оригинальные объекты
                await ShowAndSpeakAsync(chater, message, text);
                Debug.WriteLine("[ManualQueue] Воспроизведение завершено");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PlayNextFromQueueAsync Error] {ex.Message}");
            }
            finally
            {
                _isPlayingManual = false;
            }
        }

        public int QueueSize => _manualQueue.Count;

        public void ClearQueue()
        {
            _manualQueue.Clear();
            Debug.WriteLine("[ManualQueue] Очередь очищена");
            Application.Current.Dispatcher.Invoke(() =>
            {
                var vm = Application.Current.MainWindow?.DataContext as MainViewModel;
                vm?.UpdateImportantQueueCount(0);
            });
        }

        public void SavePosition(AppSettings settings)
        {
            if (_window == null) return;
            settings.ImportantOverlayTop = _isHidden ? _savedTop : _window.Top;
            settings.ImportantOverlayLeft = _isHidden ? _savedLeft : _window.Left;
            settings.ImportantOverlayWidth = _window.Width;
            settings.ImportantOverlayHeight = _window.Height;
            settings.ImportantChatMode = _currentMode;
        }

        public void LoadPosition(AppSettings settings)
        {
            if (_window == null) return;
            _window.Top = settings.ImportantOverlayTop;
            _window.Left = settings.ImportantOverlayLeft;
            _window.Width = settings.ImportantOverlayWidth;
            _window.Height = settings.ImportantOverlayHeight;
            SetDisplayMode(settings.ImportantChatMode);
        }

        public void SetAutoDisplay(bool isAuto)
        {
            Debug.WriteLine($"[ImportantService] SetAutoDisplay: {isAuto}");
        }

        public void Show() { if (_window != null) _window.Visibility = Visibility.Visible; }
        public void Hide() { if (_window != null) _window.Visibility = Visibility.Collapsed; }
        public void Toggle() { if (_window != null) _window.Visibility = _window.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; }
    }
}