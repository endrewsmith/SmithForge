using CommunityToolkit.Mvvm.ComponentModel;
using SmithForge.Main.Models;
using SmithForge.Main.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;

namespace SmithForge.Features.ImportantOverlay
{
    public partial class ImportantOverlayViewModel : ObservableObject
    {
        [ObservableProperty] private bool _isSetupMode;
        [ObservableProperty] private ChatDisplayMode _currentMode = ChatDisplayMode.AppearAndFade;

        public ObservableCollection<DisplayMessageViewModel> DisplayMessages { get; } = new();

        public ImportantOverlayViewModel()
        {
            // Подписка на обновление пользователей
            ChaterStorage.OnChaterUpdated += OnChaterUpdated;
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

        /// <summary>
        /// Показать сообщение (без звука и голоса)
        /// </summary>
        public void ShowMessage(Chater user, CommonMessage msg)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var msgVm = new DisplayMessageViewModel(user, msg);

                    string cleanText = Regex.Replace(msg.Message, @"<[^>]*>", "").Trim();
                    if (string.IsNullOrEmpty(cleanText)) return;

                    msgVm.MessageText = cleanText;
                    msgVm.DisplayTimeMs = 5000; // 5 секунд
                    ApplyModeSettings(msgVm);

                    DisplayMessages.Add(msgVm);

                    // Автоматически удаляем через 5 секунд
                    Task.Delay(5000).ContinueWith(_ =>
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            if (DisplayMessages.Contains(msgVm))
                                DisplayMessages.Remove(msgVm);
                        });
                    });
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ImportantOverlay ShowMessage Error] {ex.Message}");
                }
            });
        }

        public void ClearMessages()
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                DisplayMessages.Clear();
            });
        }

        private void OnChaterUpdated(Chater updatedChater)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                foreach (var msg in DisplayMessages)
                {
                    if (msg.User?.Id == updatedChater.Id)
                    {
                        msg.User = updatedChater;
                        msg.UpdateMessageCount();
                    }
                }
            });
        }

        public void Dispose()
        {
            ChaterStorage.OnChaterUpdated -= OnChaterUpdated;
        }
    }
}